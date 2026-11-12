# ADR-0001: Chiến lược xác thực

- **Trạng thái:** Accepted
- **Ngày:** 2026-11-12
- **Người quyết định:** Tech lead
- **Liên quan:** `docs/architecture.md`, `docs/auth-flow.md`, `docs/security.md`

## 1. Bối cảnh

Cần một portal cho hệ sinh thái ứng dụng nội bộ:

- Người dùng đăng nhập **một lần** (ưu tiên Google), sau đó thấy các app họ có quyền.
- Mỗi app sẽ chạy ở **subdomain riêng** (`app1.domain.com`), có thể khác stack
  (Next.js, .NET, Node, Python...).
- Backend chính: **C# .NET 10**; frontend: **React**.
- Triển khai trên **một VPS** bằng Docker Compose trong giai đoạn đầu.
- Cần quản trị app/quyền ngay từ giai đoạn 2 (admin UI).
- Muốn kiến trúc **sẵn sàng nâng cấp lên OIDC đầy đủ** khi quy mô tăng.

Câu hỏi: dùng giải pháp auth nào cho portal và cách các app nhận danh tính?

## 2. Các phương án đã cân nhắc

### A. Auth.js (NextAuth) làm trung tâm

- **Ưu:** hệ sinh thái JS phong phú, tích hợp Next.js nhanh.
- **Nhược:** Auth.js chủ yếu cho Node/Next.js; portal backend là .NET → phải thêm
  một service Node chỉ để làm auth, tăng độ phức tạp vận hành.
- **Kết luận:** loại.

### B. Dùng ngay Keycloak / Authentik / Zitadel

- **Ưu:** OIDC chuẩn, có refresh token, revoke, consent, key rotation, giao diện quản trị.
- **Nhược:** thêm một hệ thống lớn cần vận hành (DB riêng, nâng cấp, backup, RAM);
  với một VPS nhỏ và nhu cầu hiện tại là quá tải. Mapping role → app vẫn phải tự làm.
- **Kết luận:** để dành cho giai đoạn sau; thiết kế claim tương thích để chuyển đổi.

### C. Portal tự phát JWT + launch code (đã chọn)

- Portal dùng ASP.NET Core Identity với **Google external login** và cookie session.
- Khi user mở app, portal phát **launch code dùng một lần (60s)**.
- App backend đổi code tại `/api/auth/token` (client_id + client_secret) lấy
  **JWT RS256 (15 phút)**.
- Portal công bố **JWKS** để app verify offline.

## 3. Quyết định

Chọn **phương án C**.

Chi tiết:

| Thành phần | Quyết định |
| --- | --- |
| Đăng nhập người dùng | Google OAuth 2.0 (OIDC) qua ASP.NET Core `AddGoogle` |
| Phiên portal | Cookie `portal.session`, HttpOnly, SameSite=Lax, 7 ngày sliding |
| Danh tính cho app | Launch code một lần → JWT RS256, TTL 15 phút |
| Xác thực client | `client_id` + `client_secret` (hash HMAC-SHA256 có pepper) |
| Verify token | JWKS tại `/api/.well-known/jwks.json`, app tự verify |
| Quyền | Role → app (`app_access`), admin thấy mọi app active |
| Host | UI + API cùng host công khai, `/api/*` do Caddy proxy |
| Dev login | Chỉ khi `Auth:AllowDevLogin=true`, mặc định tắt |
| CSRF | Antiforgery header cho mọi mutation dùng cookie |

## 4. Lý do

1. **Khớp stack:** tận dụng ASP.NET Core Identity + Google handler, không thêm runtime.
2. **Cô lập session:** app ở subdomain khác không cần (và không thể) đọc cookie portal;
   launch code là cầu nối ngắn hạn.
3. **App không phụ thuộc portal mỗi request:** JWT verify offline bằng JWKS.
4. **Bảo mật theo chiều sâu:** code một lần + TTL ngắn + hash khi lưu + rate limit +
   audit. Client secret không rời backend app.
5. **Vận hành nhẹ:** một Postgres, ba container (API, web, demo) + Caddy trên VPS.
6. **Đường nâng cấp rõ:** claim `sub/email/name/role` tương thích OIDC; có thể thay
   portal bằng Keycloak/Authentik mà app gần như không đổi.

## 5. Hệ quả

### Tích cực

- Triển khai nhanh, ít thành phần.
- App tích hợp đơn giản: hai endpoint (token, JWKS) + verify JWT.
- Dễ audit vì mọi lần launch/token đều ghi log.

### Tiêu cực / đánh đổi

- **Không phải OIDC đầy đủ:** thiếu discovery, authorization code + PKCE, refresh token,
  revoke, consent, introspection.
- **Không thu hồi JWT tức thời:** phải chờ hết TTL 15 phút.
- **Một signing key:** xoay khoá cần mở rộng provider.
- **Tự vận hành bảo mật:** hash secret, CSRF, rate limit, key storage là trách nhiệm của team.
- **Không có SSO logout toàn hệ sinh thái:** mỗi app tự xoá session.

## 6. Kế hoạch nâng cấp lên OIDC đầy đủ

Khi có nhu cầu (nhiều app hơn, cần refresh/revoke, tuân thủ):

1. Công bố `/.well-known/openid-configuration` (issuer, endpoints, scopes).
2. Chuyển launch code → **authorization code** chuẩn (thêm `state`, PKCE `S256`).
3. Thêm `refresh_token`, endpoint revoke, và (tuỳ chọn) introspection.
4. Hỗ trợ **key rotation** với nhiều key trong JWKS theo `kid`.
5. Cân nhắc thay bằng Keycloak/Authentik; giữ nguyên claim `sub/email/name/role` để app
   không phải sửa nhiều.
6. Thêm quản lý consent/scope nếu app cần xin quyền chi tiết.

## 7. Tiêu chí xem lại quyết định

Xem lại ADR này khi bất kỳ điều sau đúng:

- Cần refresh token hoặc thu hồi token tức thời.
- Số app > ~10 hoặc có app bên thứ ba cần tích hợp chuẩn OIDC.
- Có yêu cầu tuân thủ (audit, consent, MFA tập trung, SCIM provisioning).
- Cần nhiều signing key / xoay khoá định kỳ.
- Một VPS không còn đủ tải → tách auth service.
