namespace PortalSso;

/// <summary>
/// Cấu hình kết nối tới Portal. Mỗi app vệ tinh có một client riêng.
/// </summary>
public sealed class PortalSsoOptions
{
    /// <summary>Base URL của Portal API, ví dụ https://portal.example.com.</summary>
    public string Url { get; set; } = "http://localhost:5080";

    /// <summary>Giá trị claim <c>iss</c> mà Portal phát hành; phải khớp <c>Jwt:Issuer</c>.</summary>
    public string Issuer { get; set; } = "https://portal.local";

    /// <summary>Client id của app, cũng là giá trị claim <c>aud</c>.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret do Portal cấp; chỉ nằm ở backend.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Thời gian cache JWKS trước khi tải lại.</summary>
    public TimeSpan JwksCacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    public void Validate()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Portal:Url phải là URL http/https hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            throw new InvalidOperationException("Portal:Issuer không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
        {
            throw new InvalidOperationException(
                "Portal:ClientId và Portal:ClientSecret là bắt buộc.");
        }
    }
}
