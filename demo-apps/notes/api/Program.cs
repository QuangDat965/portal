using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Notes.Api;
using PortalSso;

var builder = WebApplication.CreateBuilder(args);

var portalOptions = builder.Configuration.GetSection("Portal").Get<PortalSsoOptions>()
                    ?? new PortalSsoOptions();
portalOptions.Validate();

var secureCookies = builder.Configuration.GetValue("Cookie:Secure", false);
var sessionHours = builder.Configuration.GetValue("Cookie:SessionHours", 8);
var securePolicy = secureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

builder.Services.AddSingleton(portalOptions);
builder.Services.AddSingleton<PortalSsoClient>();
builder.Services.AddSingleton<NoteStore>();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "notes.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = securePolicy;
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "notes.session";
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
    app = "notes",
    name = configuration["App:Name"] ?? "Team Notes",
    description = configuration["App:Description"],
    portalUrl = configuration["Portal:FrontendUrl"] ?? "http://localhost:5173",
    colors = NoteStore.AllowedColors
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
        logger.LogWarning("Team Notes SSO callback thất bại: {Reason}", ex.Message);
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

app.MapGet("/api/notes", (NoteStore store) => Results.Ok(store.List()))
    .RequireAuthorization();

app.MapPost("/api/notes", (
        CreateNoteRequest request,
        ClaimsPrincipal user,
        NoteStore store) =>
    {
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return Results.BadRequest(new { error = "Tiêu đề không được để trống." });
        }

        if (title.Length > 160)
        {
            return Results.BadRequest(new { error = "Tiêu đề tối đa 160 ký tự." });
        }

        var body = request.Body?.Trim() ?? string.Empty;
        if (body.Length > 4000)
        {
            return Results.BadRequest(new { error = "Nội dung tối đa 4000 ký tự." });
        }

        var note = store.Create(
            title,
            body,
            NoteStore.NormalizeColor(request.Color),
            user.FindFirst("sub")?.Value ?? "unknown",
            user.FindFirst("name")?.Value ?? string.Empty);

        return Results.Created($"/api/notes/{note.Id}", note);
    })
    .RequireAuthorization()
    .WithMetadata(new RequireAntiforgeryTokenAttribute());

app.MapPut("/api/notes/{id:guid}", (
        Guid id,
        UpdateNoteRequest request,
        NoteStore store) =>
    {
        if (request.Title is { Length: > 160 })
        {
            return Results.BadRequest(new { error = "Tiêu đề tối đa 160 ký tự." });
        }

        if (request.Body is { Length: > 4000 })
        {
            return Results.BadRequest(new { error = "Nội dung tối đa 4000 ký tự." });
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();

        var note = store.Update(id, title, request.Body?.Trim(), request.Color, request.Pinned);
        return note is null ? Results.NotFound() : Results.Ok(note);
    })
    .RequireAuthorization()
    .WithMetadata(new RequireAntiforgeryTokenAttribute());

app.MapDelete("/api/notes/{id:guid}", (Guid id, NoteStore store) =>
    {
        return store.Delete(id) ? Results.NoContent() : Results.NotFound();
    })
    .RequireAuthorization()
    .WithMetadata(new RequireAntiforgeryTokenAttribute());

app.MapFallback("/api/{**path}", () => Results.NotFound(new { error = "not_found" }));
app.MapFallbackToFile("index.html");

app.Run();
