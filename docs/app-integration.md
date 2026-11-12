# Tích hợp app mới

Tài liệu cho team làm app vệ tinh (Next.js, ASP.NET, Node, Python, Go...).
App chỉ cần **hai việc**: đổi launch code lấy JWT, và xác minh JWT.

## 1. Tổng quan luồng

```
Người dùng bấm app trên portal
        │
        ▼
Portal tạo launch code (60s, dùng một lần)
        │  302
        ▼
GET https://app1.example.com/auth/callback?code=XXXX
        │
        ▼  (server-to-server)
POST https://portal.example.com/api/auth/token
     { clientId, clientSecret, code }
        │
        ▼
{ accessToken, expiresIn, user, roles }
        │
        ▼
App verify JWT bằng JWKS, tạo session riêng, redirect vào app
```

Điểm quan trọng:

- `clientSecret` chỉ nằm ở **backend** của app, không bao giờ gửi ra browser.
- Launch code **chỉ dùng một lần**, hết hạn sau **60 giây**.
- JWT sống **15 phút**; app tự quản session sau đó.
- App không thấy Google token hay cookie của portal.

## 2. Đăng ký app trong portal

Admin → **Quản trị → Ứng dụng → Thêm ứng dụng**:

| Trường | Ví dụ | Ghi chú |
| --- | --- | --- |
| Slug | `app1` | chữ thường, số, gạch ngang. Cũng là `clientId` |
| Name | `App 1` | tên hiển thị |
| Base URL | `https://app1.example.com` | không có `/` cuối |
| Callback path | `/auth/callback` | app tự xử lý path này |
| Roles | `member` | role được phép mở app |
| Status | `Active` | `Disabled` sẽ ẩn khỏi launcher |

Sau khi lưu, portal trả `clientSecret` **một lần duy nhất**:

```
clientId     = app1
clientSecret = 1d31sV2T4qaUsO8PX2rYFlmHG0tRgHPCRckV2K-lNdk
```

Lưu vào secret manager của app (`APP1_CLIENT_SECRET`). Nếu mất, dùng **Rotate secret**
(secret cũ mất hiệu lực ngay).

## 3. Cấu hình app

```env
PORTAL_URL=https://portal.example.com
CLIENT_ID=app1
CLIENT_SECRET=<secret>
```

Không cần cấu hình Google, không cần biết user store của portal.

## 4. Bước 1 — đổi code lấy token

Server-side, ngay khi nhận callback:

```http
POST /api/auth/token
Content-Type: application/json

{
  "clientId": "app1",
  "clientSecret": "...",
  "code": "XXXX"
}
```

Thành công `200`:

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": {
    "id": "77097ea6-3815-4b07-a91f-9660ae8c667c",
    "email": "user@example.com",
    "name": "Người dùng",
    "avatarUrl": "https://..."
  },
  "roles": ["member"]
}
```

Thất bại `400 { "error": "invalid_grant" }`. Nguyên nhân thường gặp: code đã dùng,
quá 60s, hoặc secret sai. Khi lỗi, hiển thị thông báo và cho user quay lại portal.

### Ví dụ Node (fetch)

```js
async function exchangeCode(code) {
  const response = await fetch(`${process.env.PORTAL_URL}/api/auth/token`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      clientId: process.env.CLIENT_ID,
      clientSecret: process.env.CLIENT_SECRET,
      code,
    }),
  })

  if (!response.ok) throw new Error('invalid_grant')
  return response.json()
}
```

### Ví dụ .NET

```csharp
public sealed record TokenResponse(string AccessToken, int ExpiresIn, UserDto User, string[] Roles);

var http = httpClientFactory.CreateClient("portal");
var response = await http.PostAsJsonAsync("/api/auth/token", new
{
    clientId = configuration["Portal:ClientId"],
    clientSecret = configuration["Portal:ClientSecret"],
    code
});

response.EnsureSuccessStatusCode();
var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
```

## 5. Bước 2 — xác minh JWT

### Claim

| Claim | Ý nghĩa |
| --- | --- |
| `iss` | URL portal (`Jwt:Issuer`) |
| `aud` | `clientId` của app |
| `sub` | ID user |
| `email` | email |
| `name` | tên hiển thị |
| `role` | role (có thể lặp nhiều lần) |
| `app_id` | ID app |
| `app_slug` | slug app |
| `jti` | ID token |
| `iat`, `nbf`, `exp` | thời gian |

Thuật toán: **RS256**. Public key:

```
GET https://portal.example.com/api/.well-known/jwks.json
```

### Quy tắc verify (bắt buộc)

1. Chữ ký RS256 hợp lệ với key có `kid` khớp header.
2. `iss` = `PORTAL_URL`.
3. `aud` = `CLIENT_ID` của app.
4. `exp` chưa qua, `nbf` đã tới (cho phép clock skew nhỏ ~60s).
5. Chỉ chấp nhận thuật toán RS256, **không** tin `alg` do client gửi.
6. Cache JWKS theo `kid`; refresh khi gặp `kid` lạ (xoay khoá).

### Ví dụ Node (không dependency)

```js
import crypto from 'node:crypto'

let jwks

async function verify(token, { issuer, audience }) {
  const [h, p, s] = token.split('.')
  if (!h || !p || !s) throw new Error('Malformed JWT')

  const header = JSON.parse(Buffer.from(h, 'base64url').toString())
  const payload = JSON.parse(Buffer.from(p, 'base64url').toString())

  if (header.alg !== 'RS256') throw new Error('Unexpected alg')

  if (!jwks) {
    jwks = await (await fetch(`${issuer}/api/.well-known/jwks.json`)).json()
  }

  let jwk = jwks.keys.find((k) => k.kid === header.kid)
  if (!jwk) {
    jwks = await (await fetch(`${issuer}/api/.well-known/jwks.json`)).json()
    jwk = jwks.keys.find((k) => k.kid === header.kid)
  }
  if (!jwk) throw new Error('Unknown kid')

  const ok = crypto.verify(
    'RSA-SHA256',
    Buffer.from(`${h}.${p}`),
    crypto.createPublicKey({ key: jwk, format: 'jwk' }),
    Buffer.from(s, 'base64url'),
  )
  if (!ok) throw new Error('Bad signature')

  if (payload.iss !== issuer) throw new Error('Bad issuer')
  if (payload.aud !== audience) throw new Error('Bad audience')
  if (payload.exp * 1000 < Date.now() - 60_000) throw new Error('Expired')

  return payload
}
```

### Ví dụ .NET

```csharp
builder.Services
    .AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Portal:Url"];        // nếu có OIDC discovery
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = configuration["Portal:Url"],
            ValidAudience = configuration["Portal:ClientId"],
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(60),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };
        options.MetadataAddress = ""; // hiện chưa có discovery; cấu hình JWKS thủ công:
        options.Configuration = new OpenIdConnectConfiguration
        {
            JwksUri = $"{configuration["Portal:Url"]}/api/.well-known/jwks.json"
        };
    });
```

> Portal hiện **chưa** công bố `/.well-known/openid-configuration`. Với .NET, có thể
> dùng `JwtSecurityTokenHandler` + `JsonWebKeySet` tải từ JWKS, hoặc thêm discovery
> endpoint khi nâng cấp lên OIDC đầy đủ (xem ADR-0001).

## 6. Tạo session trong app

Sau khi verify:

1. Tìm/tạo user nội bộ theo `sub` (không dùng email làm khoá chính — email có thể đổi).
2. Ánh xạ `role[]` sang quyền của app nếu cần.
3. Tạo session riêng của app (cookie HttpOnly) hoặc JWT nội bộ.
4. Redirect vào trang chính.

JWT của portal **không dùng làm session dài hạn** (15 phút). Nếu cần refresh, cho user
quay lại portal mở lại app (launch code mới).

## 7. Bảo mật phía app

- Giữ `clientSecret` trong env/secret manager; không đưa vào bundle frontend.
- Chỉ trao đổi code ở backend; không log code, secret hay token.
- Callback không yêu cầu session trước — code chính là bằng chứng.
- Bật HTTPS; cookie `Secure`, `HttpOnly`, `SameSite=Lax`.
- Kiểm tra `aud` để JWT của app khác không dùng được.
- Xoá session app khi user đăng xuất; không cần gọi portal (portal không thu hồi JWT).

## 8. Checklist tích hợp

- [ ] App đã tạo trong Admin UI, status `Active`, đúng role.
- [ ] `BaseUrl` + `CallbackPath` đúng URL thật.
- [ ] `clientId` / `clientSecret` cấu hình ở backend app.
- [ ] Callback đổi code trong **một** request, xử lý lỗi `invalid_grant`.
- [ ] Verify đủ `alg`, `iss`, `aud`, `exp`.
- [ ] JWKS cache + refresh theo `kid`.
- [ ] Session riêng của app, có logout.
- [ ] Đã test: mở app từ portal → thấy đúng user/role; mở lại lần nữa vẫn OK.
- [ ] Đã test: user không có role → không thấy app ở launcher và launch trả `404`.
