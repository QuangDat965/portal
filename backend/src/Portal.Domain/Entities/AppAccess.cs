namespace Portal.Domain.Entities;

/// <summary>
/// Quyền truy cập app theo role. User có role nào thì được mở các app gán cho role đó.
/// </summary>
public class AppAccess
{
    public Guid AppId { get; set; }
    public RegisteredApp App { get; set; } = null!;
    public Guid RoleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
