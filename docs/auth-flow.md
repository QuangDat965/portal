# Luồng xác thực

## 1. Đăng nhập Google

```
Browser            Portal API                 Google
   │  GET /api/auth/google   │                    │
   ├────────────────────────▶│  redirect (state)  │
   │                         ├───────────────────▶│
   │                         │◀── code + state ───┤
   │  GET /api/auth/google/callback              │
   ├────────────────────────▶│  đổi code lấy id_token
   │                         ├───────────────────▶│ Google token endpoint
   │                         │◀───────────────────┤
   │                         │ upsert user, gán role member
   │◀── Set-Cookie portal.session ────────────────┤
   │  302 /api/auth/google/complete               │
   │  302 {FrontendUrl}/apps                      │
```

- Endpoint challenge: `GET /api/auth/google`.
- Callback do Google handler xử lý: `/api/auth/google/callback` (phải khai báo đúng trong Google Console).
- Sau khi Google trả thông tin, `OnTicketReceived`:
  - Tìm user theo `(provider=Google, subject)`.
  - Nếu chưa có, tìm theo email; nếu vẫn chưa có thì tạo user + gán role `member`.
  - Cập nhật `DisplayName`, `AvatarUrl`, `LastLoginAt`.
  - Tạo principal mới với `NameIdentifier`, `Name`, `Email`, `avatar_url`, `role[]`.
- Handler ghi cookie `portal.session` và redirect về `/api/auth/google/complete`, endpoint này redirect tiếp về SPA.

### Cookie session

| Thuộc tính | Giá trị |
| --- | --- |
| Tên | `portal.session` |
| HttpOnly | có |
| SameSite | Lax |
| Secure | theo scheme request (production bắt buộc HTTPS) |
| Thời hạn | 7 ngày, sliding |
| Phạm vi | host portal |

## 2. Đăng nhập bằng email/mật khẩu

```
POST /api/auth/password-login      (CSRF header + cookie portal.csrf)
Content-Type: application/json

{ "email": "admin@example.com", "password": "..." }
```

- `email` nhận **email hoặc username** (user tạo từ Google có `UserName = email`).
- Xác thực bằng `SignInManager.PasswordSignInAsync`; thành công ghi cookie
  `portal.session` như luồng Google.
- Sai thông tin → `401` với **cùng một thông báo** (không lộ email có tồn tại hay không).
- Khoá tạm 5 phút sau 5 lần nhập sai → `423`.
- Rate limit `login` 10 request/phút/IP.
- User chỉ có Google login (chưa có `PasswordHash`) không đăng nhập được bằng mật khẩu
  cho tới khi admin đặt mật khẩu tại `PUT /api/admin/users/{id}/password`.
- Admin có thể đặt mật khẩu seed qua `Seed:AdminPassword` (mặc định chỉ đặt ở
  `appsettings.Development.json`).

## 3. Dev login

Chỉ bật khi `Auth:AllowDevLogin=true` **và** môi trường phát triển.

```
POST /api/auth/dev-login
{ "email": "admin@example.com", "name": "Admin" }
```

- Nếu user chưa tồn tại: tạo user + role `member`.
- `admin@example.com` được seed sẵn role `admin`.
- Không có mật khẩu; đây là công cụ local, không dùng production.

## 4. Lấy danh sách app

```
GET /api/me/apps            (cookie session)
```

- Admin: mọi app `Active`.
- User thường: app `Active` có `app_access.role_id` nằm trong role của user.
- Kết quả sắp xếp theo `sort_order`, `name`.

## 5. Launch code

```
POST /api/apps/{slug}/launch      (cookie + CSRF header)
```

Điều kiện:

- User đã đăng nhập.
- App tồn tại và `status = Active`.
- User có quyền (admin bỏ qua kiểm tra role).
- Rate limit: 30 request/phút/IP.

Kết quả:

```json
{
  "redirectUrl": "https://app1.example.com/auth/callback?code=XXXX",
  "expiresInSeconds": 60
}
```

- Code: 32 byte ngẫu nhiên, encode base64url.
- DB chỉ lưu `HMAC-SHA256(code, pepper)`.
- Hết hạn sau 60 giây, dùng một lần.
- Ghi `audit_logs` action `app.launch`.

## 6. Đổi code lấy token

```
POST /api/auth/token
Content-Type: application/json

{
  "clientId": "app1",
  "clientSecret": "...",
  "code": "XXXX"
}
```

- Xác thực client bằng so sánh constant-time trên hash.
- Code phải thuộc đúng app của client, chưa dùng, chưa hết hạn.
- Đánh dấu dùng bằng `ExecuteUpdate` có điều kiện `used_at IS NULL` (atomic).
- Rate limit: 60 request/phút/IP.

Thành công `200`:

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": { "id": "...", "email": "...", "name": "...", "avatarUrl": null },
  "roles": ["member"]
}
```

Thất bại: `400 { "error": "invalid_grant" }` cho mọi trường hợp (không tiết lộ lý do).

## 7. JWT

| Claim | Ý nghĩa |
| --- | --- |
| `iss` | `Jwt:Issuer` (URL portal) |
| `aud` | `client_id` của app |
| `sub` | ID user (Guid) |
| `email` | email user |
| `name` | tên hiển thị |
| `role` | một hoặc nhiều role |
| `app_id` | ID app |
| `app_slug` | slug app |
| `jti` | ID token |
| `iat`, `nbf`, `exp` | thời gian; TTL mặc định 15 phút |

Ký bằng **RS256**. Public key công bố tại:

```
GET /api/.well-known/jwks.json
```

## 8. App xác minh JWT

App cần:

1. Tải JWKS từ portal (cache theo `kid`).
2. Verify chữ ký RS256.
3. Kiểm tra `iss`, `aud` (bằng client id của app), `exp`.
4. Đọc `sub`, `email`, `role[]` để tạo session nội bộ.

Ví dụ Node:

```js
import crypto from 'node:crypto'

const jwks = await (await fetch(`${PORTAL}/api/.well-known/jwks.json`)).json()
const [h, p, s] = token.split('.')
const header = JSON.parse(Buffer.from(h, 'base64url'))
const key = crypto.createPublicKey({
  key: jwks.keys.find((k) => k.kid === header.kid),
  format: 'jwk',
})
const ok = crypto.verify(
  'RSA-SHA256',
  Buffer.from(`${h}.${p}`),
  key,
  Buffer.from(s, 'base64url'),
)
```

Xem code đầy đủ trong `demo-app/server.mjs`.

## 9. Đăng xuất

```
POST /api/auth/logout      (cookie + CSRF header)
```

- Xoá cookie portal.
- Không thu hồi JWT đã phát (TTL ngắn); app tự xoá session của mình.
- Muốn thu hồi tức thời cần thêm token revocation/refresh ở giai đoạn OIDC.
