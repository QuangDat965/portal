# Bảo mật

Tài liệu này mô tả mô hình đe doạ và các biện pháp đang áp dụng. Cập nhật khi thay đổi
luồng auth, claim, hoặc mô hình quyền.

## 1. Tài sản cần bảo vệ

| Tài sản | Nơi lưu | Mức độ |
| --- | --- | --- |
| Session người dùng | cookie `portal.session` | cao |
| Mật khẩu người dùng (hash Identity) | `AspNetUsers.PasswordHash` | cao |
| Client secret của app | hash HMAC trong `app_clients` | cao |
| Launch code | hash HMAC trong `launch_codes` | cao |
| JWT private key (RS256) | file/volume `/data/keys` hoặc env | rất cao |
| `Security:SecretPepper` | env/secret manager | rất cao |
| DataProtection keys | volume `/data/keys` | cao |
| Dữ liệu user/role/quyền | PostgreSQL | cao |

## 2. Mô hình đe doạ và biện pháp

### 2.1 Đánh cắp session (XSS)

- Cookie `portal.session` là **HttpOnly** → JS không đọc được.
- React escape mặc định; không dùng `dangerouslySetInnerHTML`.
- `Content-Type` đúng; Caddy thêm `X-Content-Type-Options: nosniff`,
  `X-Frame-Options: DENY`, `Referrer-Policy`.
- Token/secret không lưu `localStorage`. Chỉ lưu lựa chọn ngôn ngữ.

### 2.2 CSRF

- Mọi mutation dùng cookie yêu cầu antiforgery (`X-CSRF-TOKEN` + cookie `portal.csrf`).
- Endpoint gọi `ValidateAntiforgeryAsync()`; thiếu/sai → `400`.
- Cookie `SameSite=Lax` giảm rủi ro cross-site; CSRF header là lớp bắt buộc.
- `POST /api/auth/token` là server-to-server, không dùng cookie, không cần CSRF.
- `POST /api/auth/dev-login` không yêu cầu CSRF vì mục đích tạo session đầu tiên;
  endpoint này **phải tắt** ở production.
- `POST /api/auth/password-login` **có** yêu cầu CSRF để chống login CSRF; client lấy
  token qua `GET /api/auth/csrf` trước khi gửi mật khẩu.

### 2.3 Chiếm đoạt launch code

- Code 32 byte ngẫu nhiên (256-bit), không đoán được.
- Chỉ lưu hash HMAC-SHA256; DB bị đọc cũng không lấy được code.
- TTL 60 giây.
- Dùng một lần, đánh dấu **atomic**:
  `UPDATE ... SET used_at = now WHERE id = ? AND used_at IS NULL`.
  Hai request song song chỉ một thành công.
- Code gắn `AppId`; client của app khác không đổi được.
- Rate limit `token` 60/phút/IP.

### 2.4 Lộ client secret

- Chỉ lưu hash HMAC-SHA256 với pepper; so sánh constant-time.
- Plaintext chỉ trả **một lần** khi tạo app hoặc rotate.
- Rotate làm secret cũ mất hiệu lực ngay.
- Secret chỉ dùng server-to-server; không bao giờ gửi xuống browser.

### 2.5 JWT bị giả mạo

- Ký RS256 bằng private key không rời server.
- App verify bằng public key từ JWKS, kiểm tra `iss`, `aud`, `exp`, `alg`.
- TTL ngắn (15 phút) giới hạn thiệt hại nếu lộ token.
- `jti` cho phép đối chiếu/audit khi cần.
- Chưa có cơ chế thu hồi token tức thời (xem mục 6).

### 2.6 Leo thang quyền

- `user_id`, `role`, `app_id` **luôn** suy ra từ session/JWT ở server; không tin body/query.
- Entitlement kiểm tra ở server trước khi phát launch code.
- App không tồn tại / không active / không có quyền đều trả **cùng** `404` → không lộ
  sự tồn tại của app.
- Admin endpoint yêu cầu policy `AdminOnly` (role `admin`).
- Không thể tự bỏ quyền admin của mình; không thể bỏ admin cuối cùng.
- User mới từ Google mặc định role `member`.

### 2.7 Lạm dụng / DoS

- Rate limit theo IP: `launch` 30/phút, `token` 60/phút, `login` 10/phút.
  Ngoài ra Identity khoá tài khoản 5 phút sau 5 lần nhập sai.
- JSON body kích thước mặc định của ASP.NET Core.
- Truy vấn có `Take`/clamp (`users` ≤ 200, `audit` ≤ 200).
- Có thể thêm rate limit toàn cục hoặc tại Caddy khi cần.

### 2.8 Rò rỉ qua log

- Serilog chỉ log request summary.
- Không log token, secret, launch code, cookie, header Authorization.
- Response lỗi không chứa stack trace (`UseExceptionHandler` + ProblemDetails).
- Audit log lưu metadata không nhạy cảm (slug, email + role), không lưu code/secret.

### 2.9 Cấu hình sai khi deploy

- `Auth:AllowDevLogin=false` ở production (mặc định trong `appsettings.json`).
- `Security:SecretPepper` phải mạnh và cố định; đổi pepper phá toàn bộ secret cũ.
- `Jwt:Issuer` phải là URL HTTPS công khai.
- `Cookie.SecurePolicy = SameAsRequest` → production **bắt buộc** HTTPS (Caddy lo TLS).
- Không commit `.env`, `keys/`, `*.pem` (đã có `.gitignore`).
- DataProtection keys persist qua volume, tránh mất session sau deploy.

### 2.10 Mật khẩu cục bộ

- Mật khẩu do ASP.NET Core Identity hash (PBKDF2, salt riêng cho từng user).
- Policy: tối thiểu 12 ký tự; không bắt buộc ký tự đặc biệt (khuyến khích passphrase).
- Sai 5 lần → khoá 5 phút (`Lockout`); rate limit `login` 10/phút/IP.
- Lỗi đăng nhập trả `401` với thông báo chung, không phân biệt email tồn tại hay không.
- Không log mật khẩu; không trả `PasswordHash` qua API.
- Chỉ admin đặt/đổi mật khẩu (`PUT /api/admin/users/{id}/password`); thao tác được audit.
- `Seed:AdminPassword` chỉ nên đặt ở môi trường dev hoặc qua secret manager khi deploy.

## 3. Ranh giới tin cậy

```
Browser  ──(cookie portal)──▶  Portal API  ──(client_id/secret)──▶  DB
   │                              │
   │  (launch code, 1 lần)        │  (JWT RS256)
   ▼                              ▼
App A (subdomain A)          App B (subdomain B)
```

- App **không tin** dữ liệu user do browser gửi; chỉ tin JWT đã verify.
- Portal **không tin** tham số app/user từ client.
- App không chia sẻ cookie với portal hay app khác.

## 4. Bảo vệ khoá

- Private key JWT: sinh bằng RSA 2048+. Production nên cấp qua secret manager
  (`Jwt__PrivateKeyPem`) hoặc file chỉ app đọc được.
- Không để key trong image Docker; dùng volume `keys`.
- Có kế hoạch xoay khoá: JWKS hỗ trợ nhiều key theo `kid` (hiện code trả một key;
  khi cần xoay, mở rộng provider để giữ key cũ trong thời gian chuyển tiếp).
- `SECRET_PEPPER` và DB password backup an toàn, tách khỏi backup DB.

## 5. Checklist review bảo mật

Khi thêm endpoint/feature:

- [ ] Có `RequireAuthorization` (và `AdminOnly` nếu là admin)?
- [ ] Mutation có validate antiforgery?
- [ ] Dữ liệu nhạy cảm suy ra từ server, không từ input?
- [ ] Có rate limit nếu endpoint có thể bị lạm dụng?
- [ ] Không log/trả secret, token, code?
- [ ] Response lỗi không lộ thông tin (dùng 404 chung cho tài nguyên không được phép)?
- [ ] Có audit log cho hành động quản trị quan trọng?
- [ ] i18n và thông báo lỗi không chứa chi tiết nội bộ?

## 6. Giới hạn hiện tại / việc cần làm khi nâng cấp

| Giới hạn | Hướng xử lý |
| --- | --- |
| Chưa có refresh token / revoke | Thêm khi chuyển sang OIDC đầy đủ |
| JWT không thu hồi tức thời | TTL ngắn; thêm denylist `jti` nếu cần |
| Chưa có discovery document | Thêm `/.well-known/openid-configuration` |
| Một signing key | Mở rộng `SigningKeyProvider` cho key rotation |
| Rate limit theo IP đơn thuần | Kết hợp user/app, hoặc giới hạn ở Caddy/WAF |
| Chưa có 2FA riêng | Dựa vào Google (có thể bật 2FA tại Google); user mật khẩu cục bộ chưa có 2FA |
| Chưa có phân quyền chi tiết trong app | App tự map `role[]` sang quyền nội bộ |
