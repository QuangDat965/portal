namespace PortalSso;

/// <summary>
/// Danh tính đã được xác minh từ JWT do Portal phát hành.
/// </summary>
public sealed record PortalIdentity(
    string Subject,
    string Email,
    string Name,
    IReadOnlyList<string> Roles,
    DateTimeOffset ExpiresAt);
