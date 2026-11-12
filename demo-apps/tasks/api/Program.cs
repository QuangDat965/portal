using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PortalSso;
using Tasks.Api;

var builder = WebApplication.CreateBuilder(args);

var portalOptions = builder.Configuration.GetSection("Portal").Get<PortalSsoOptions>()
                    ?? new PortalSsoOptions();
portalOptions.Validate();

var secureCookies = builder.Configuration.GetValue("Cookie:Secure", false);
var sessionHours = builder.Configuration.GetValue("Cookie:SessionHours", 8);
var securePolicy = secureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

builder.Services.AddSingleton(portalOptions);
builder.Services.AddSingleton<PortalSsoClient>();
builder.Services.AddSingleton<TaskStore>();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "tasks.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = securePolicy;
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "tasks.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = securePolicy;
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/info", (IConfiguration configuration) => Results.Ok(new
{
    app = "tasks",
    name = configuration["App:Name"] ?? "Taskboard",
    description = configuration["App:Description"],
    portalUrl = configuration["Portal:FrontendUrl"] ?? "http://localhost:5173"
}));

app.MapGet("/auth/callback", async (
    string? code,
    HttpContext context,
    PortalSsoClient sso,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(code))
    {
        return Results.Redirect("/?error=missing_code");
    }

    PortalIdentity identity;
    try
    {
        identity = await sso.RedeemLaunchCodeAsync(code, context.RequestAborted);
    }
    catch (PortalSsoException ex)
    {
        logger.LogWarning("Taskboard SSO callback thất bại: {Reason}", ex.Message);
        return Results.Redirect("/?error=sso");
    }

    var claims = new List<Claim>
    {
        new("sub", identity.Subject),
        new("email", identity.Email),
        new("name", identity.Name)
    };
    claims.AddRange(identity.Roles.Select(role => new Claim("role", role)));

    var principal = new ClaimsPrincipal(new ClaimsIdentity(
        claims, CookieAuthenticationDefaults.AuthenticationScheme));

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(sessionHours)
        });

    return Results.Redirect("/");
}).AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
})
.RequireAuthorization()
.WithMetadata(new RequireAntiforgeryTokenAttribute());

app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }))
    .RequireAuthorization();

app.MapGet("/api/me", (ClaimsPrincipal user) => Results.Ok(new
{
    id = user.FindFirst("sub")?.Value ?? string.Empty,
    email = user.FindFirst("email")?.Value ?? string.Empty,
    name = user.FindFirst("name")?.Value ?? string.Empty,
    roles = user.FindAll("role").Select(c => c.Value).ToArray()
})).RequireAuthorization();

app.MapGet("/api/tasks", (TaskStore store) => Results.Ok(store.List()))
    .RequireAuthorization();

app.MapPost("/api/tasks", (
        CreateTaskRequest request,
        ClaimsPrincipal user,
        TaskStore store) =>
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return Results.BadRequest(new { error = "Tiêu đề không được để trống." });
        }

        if (title.Length > 200)
        {
            return Results.BadRequest(new { error = "Tiêu đề tối đa 200 ký tự." });
        }

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (notes is { Length: > 2000 })
        {
            return Results.BadRequest(new { error = "Ghi chú tối đa 2000 ký tự." });
        }

        var task = store.Create(
            title,
            notes,
            user.FindFirst("sub")?.Value ?? "unknown",
            user.FindFirst("name")?.Value ?? string.Empty);

        return Results.Created($"/api/tasks/{task.Id}", task);
    })
    .RequireAuthorization()
    .WithMetadata(new RequireAntiforgeryTokenAttribute());

app.MapPut("/api/tasks/{id:guid}", (
        Guid id,
        UpdateTaskRequest request,
        TaskStore store) =>
    {
        if (request.Title is { Length: > 200 })
        {
            return Results.BadRequest(new { error = "Tiêu đề tối đa 200 ký tự." });
        }

        if (request.Notes is { Length: > 2000 })
        {
            return Results.BadRequest(new { error = "Ghi chú tối đa 2000 ký tự." });
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();

        var task = store.Update(id, title, request.Notes?.Trim(), request.Done);
        return task is null ? Results.NotFound() : Results.Ok(task);
    })
    .RequireAuthorization()
    .WithMetadata(new RequireAntiforgeryTokenAttribute());

app.MapDelete("/api/tasks/{id:guid}", (Guid id, TaskStore store) =>
    {
        return store.Delete(id) ? Results.NoContent() : Results.NotFound();
    })
    .RequireAuthorization()
    .WithMetadata(new RequireAntiforgeryTokenAttribute());

app.MapFallback("/api/{**path}", () => Results.NotFound(new { error = "not_found" }));
app.MapFallbackToFile("index.html");

app.Run();
