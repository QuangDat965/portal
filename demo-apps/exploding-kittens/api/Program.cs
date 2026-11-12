using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using ExplodingKittens.Api;
using ExplodingKittens.Api.Endpoints;
using ExplodingKittens.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using PortalSso;

var builder = WebApplication.CreateBuilder(args);

// ---- Portal SSO ----
var portalOptions = builder.Configuration.GetSection("Portal").Get<PortalSsoOptions>()
                    ?? new PortalSsoOptions();
portalOptions.Validate();

builder.Services.AddSingleton(portalOptions);
builder.Services.AddSingleton<PortalSsoClient>();

// ---- Database ----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"),
        o => o.MigrationsHistoryTable("__EFMigrationsHistory", "exploding_kittens")));

builder.Services.AddScoped<GameEngineService>();

// ---- Auth cookie của app ----
var secureCookies = builder.Configuration.GetValue("Cookie:Secure", false);
var sessionHours = builder.Configuration.GetValue("Cookie:SessionHours", 8);
var securePolicy = secureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "ek.csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = securePolicy;
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ek.session";
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

// ---- SignalR ----
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false;
}).AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();
app.UseExceptionHandler(handler => handler.Run(context =>
    Results.Problem(statusCode: 500, title: "An unexpected error occurred.").ExecuteAsync(context)));

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGameEndpoints();
app.MapAuthEndpoints();
app.MapHub<GameHub>("/api/hub/game");
app.MapFallback("/api/{**path}", () => Results.NotFound(new { error = "not_found" }));
app.MapFallbackToFile("index.html");

// ---- Migrate + seed ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await GameDbInitializer.InitializeAsync(db, logger);
}

app.Run();

public partial class Program;
