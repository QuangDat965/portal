namespace PortalSso;

/// <summary>
/// Lỗi khi đổi launch code hoặc xác minh JWT. Message an toàn để trả cho người dùng,
/// không chứa token, secret hay launch code.
/// </summary>
public sealed class PortalSsoException : Exception
{
    public PortalSsoException(string message) : base(message)
    {
    }

    public PortalSsoException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
