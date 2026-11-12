namespace Portal.Domain.Entities;

/// <summary>
/// Thông tin client để app backend đổi launch code lấy access token.
/// Secret chỉ lưu dạng hash.
/// </summary>
public class AppClient
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppId { get; set; }
    public RegisteredApp App { get; set; } = null!;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecretHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RotatedAt { get; set; }
}
