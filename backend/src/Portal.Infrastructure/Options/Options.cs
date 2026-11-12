namespace Portal.Infrastructure.Options;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "https://portal.local";
    public int AccessTokenMinutes { get; set; } = 15;
}

public sealed class AuthOptions
{
    public bool AllowDevLogin { get; set; }
    public string FrontendUrl { get; set; } = "http://localhost:5173";
}

public sealed class SeedOptions
{
    public bool DemoData { get; set; } = true;
    public string AdminEmail { get; set; } = "admin@example.com";
    public string AdminName { get; set; } = "Portal Admin";
    public string AdminPassword { get; set; } = string.Empty;
    public string DemoAppUrl { get; set; } = "http://localhost:5174";
    public string DemoAppClientSecret { get; set; } = "demo-secret-change-me";
}

public sealed class SecurityOptions
{
    public string SecretPepper { get; set; } = "change-me-in-production";
}
