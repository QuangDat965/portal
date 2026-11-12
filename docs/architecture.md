# Kiến trúc

## 1. Tổng quan

```
                        ┌────────────────────────────────────────────┐
                        │            portal.domain.com               │
                        │  React SPA  ──  /api/*  ──▶  ASP.NET Core  │
                        │                                   │        │
                        │                              PostgreSQL    │
                        └───────────────────┬────────────────────────┘
                                            │ launch code (60s)
                                            ▼
                        ┌────────────────────────────────────────────┐
                        │            app1.domain.com                 │
                        │  App backend ──▶ POST /api/auth/token      │
                        │       ▲                                    │
                        │       └── JWT 15m (verify bằng JWKS)       │
                        └────────────────────────────────────────────┘
```

- **Portal** là nơi duy nhất người dùng đăng nhập.
- **Portal API** là nguồn sự thật về user, role, app và quyền truy cập.
- **App vệ tinh** không cần biết Google; chỉ cần đổi launch code lấy JWT và xác minh chữ ký.
- Mỗi app chạy ở subdomain riêng, session riêng.

## 2. Thành phần

### Backend (.NET 10)

| Project | Trách nhiệm |
| --- | --- |
| `Portal.Domain` | Entity: `RegisteredApp`, `AppAccess`, `AppClient`, `LaunchCode`, `AuditLog`; hằng số role |
| `Portal.Application` | DTO và interface (`IEntitlementService`, `ILaunchService`, `ITokenIssuer`, ...) |
| `Portal.Infrastructure` | EF Core + Identity, JWT signing, launch service, entitlement, seed |
| `Portal.Api` | Minimal API, Google OAuth, cookie auth, CSRF, rate limit, admin endpoints |

### Frontend (React 19 + Vite)

- `LoginPage`: Google login + email/mật khẩu + dev login.
- `LauncherPage`: grid app theo quyền.
- `ProfilePage`: thông tin user/role.
- `AdminPage`: tab Apps / Users / Audit.
- TanStack Query quản lý server state; i18next cho vi/en.

### Hạ tầng

- PostgreSQL 17.
- Caddy làm reverse proxy + TLS.
- `/api/*` được proxy về container API, phần còn lại về container web.
- Cookie session là first-party vì UI và API cùng host công khai.

## 3. Quyết định thiết kế

| Quyết định | Lý do |
| --- | --- |
| Portal tự phát JWT thay vì dùng Keycloak ngay | MVP nhanh, vẫn giữ đúng mô hình OIDC để nâng cấp sau |
| Launch code dùng một lần thay vì chia sẻ cookie | App ở subdomain/domain khác vẫn tích hợp được, không lộ session portal |
| Quyền theo role → app | Dễ quản lý khi số user tăng; admin bỏ qua kiểm tra để thấy mọi app |
| `/api` cùng host với UI | Cookie first-party, không cần CORS phức tạp ở production |
| JWT RS256 + JWKS | App tự verify offline, không gọi portal mỗi request |
| Access token 15 phút, không refresh ở MVP | Giảm rủi ro; app có thể yêu cầu user mở lại từ portal |

## 4. Vòng đời một request

### Người dùng mở portal

1. SPA gọi `GET /api/me` và `GET /api/me/apps`.
2. API đọc cookie `portal.session`, trả user + app đã lọc theo role.
3. SPA render grid.

### Người dùng mở app

1. SPA gọi `POST /api/apps/{slug}/launch` kèm CSRF header.
2. API kiểm tra app active + entitlement, ghi audit, tạo launch code (hash trong DB).
3. API trả `redirectUrl` chứa code.
4. Trình duyệt chuyển sang app.

### App nhận danh tính

1. App backend nhận `code` tại callback.
2. Gọi `POST /api/auth/token` với `client_id`, `client_secret`, `code`.
3. Portal đánh dấu code đã dùng (atomic), phát JWT.
4. App verify JWT bằng JWKS và tạo session riêng.

## 5. Mô hình tin cậy

- Trình duyệt chỉ giữ cookie portal cho host portal.
- App không bao giờ thấy Google token hay cookie portal.
- `client_secret` chỉ nằm ở backend của app.
- JWT chứa `sub`, `email`, `name`, `role[]`, `app_id`, `app_slug`, `aud`, `iss`, `exp`.
- App phải kiểm tra `aud` khớp client của mình và `iss` khớp portal.

## 6. Đường nâng cấp lên OIDC đầy đủ

Khi cần:

1. Thêm `/.well-known/openid-configuration` mô tả issuer, authorization/token/jwks endpoint.
2. Đổi launch code thành authorization code chuẩn (có `state`, PKCE).
3. Thêm refresh token và endpoint revoke.
4. Có thể thay bằng Keycloak/Authentik mà giữ nguyên claim `sub/email/role`.

Xem `docs/decisions/ADR-0001-auth-strategy.md`.
