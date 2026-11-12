namespace Portal.Domain.Entities;

/// <summary>
/// Một ứng dụng trong hệ sinh thái. Mỗi app chạy ở subdomain riêng và nhận danh tính
/// người dùng thông qua launch code + JWT do portal phát hành.
/// </summary>
public class RegisteredApp
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = "/auth/callback";
    public AppStatus Status { get; set; } = AppStatus.Active;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AppAccess> Access { get; set; } = new List<AppAccess>();
    public ICollection<AppClient> Clients { get; set; } = new List<AppClient>();
}

public enum AppStatus
{
    Active = 0,
    Disabled = 1
}
