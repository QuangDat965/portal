namespace Portal.Domain.Entities;

/// <summary>
/// Mã dùng một lần để chuyển phiên từ portal sang app. Chỉ lưu hash của mã.
/// </summary>
public class LaunchCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CodeHash { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid AppId { get; set; }
    public RegisteredApp App { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
