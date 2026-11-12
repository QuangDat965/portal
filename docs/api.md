# API

Base URL: `/api` (dev: `http://localhost:5080/api`). Định dạng JSON, camelCase.

- **Cookie auth**: `portal.session` (HttpOnly). Dùng cho endpoint `/api/me`, `/api/apps/*`, `/api/admin/*`.
- **CSRF**: mọi request ghi (POST/PUT/DELETE) dùng cookie phải gửi header
  `X-CSRF-TOKEN`, lấy từ `GET /api/auth/csrf`.
- **Bearer auth**: không áp dụng cho portal API. JWT chỉ dùng cho app vệ tinh.

Tài liệu tương tác (dev): `/scalar/v1` (Scalar UI) và `/openapi/v1.json`.

## 1. Auth

### `GET /api/auth/google`

Bắt đầu luồng OAuth với Google. Trả `302` sang Google. Nếu chưa cấu hình
`Auth:Google:ClientId` → `503`.

### `GET /api/auth/google/callback`

Google gọi lại sau khi user đồng ý. Đây là **Authorized redirect URI** cần khai báo
trong Google Console. Handler tạo/cập nhật user, ghi cookie session, rồi redirect
sang `/api/auth/google/complete`.

### `GET /api/auth/google/complete`

Đọc session sau callback và redirect về `Auth:FrontendUrl/apps`
(hoặc `/login?error=google`).

### `POST /api/auth/dev-login`

Chỉ hoạt động khi `Auth:AllowDevLogin=true`. Không cần CSRF (dùng để lấy session đầu tiên).

```json
{ "email": "admin@example.com", "name": "Admin" }
```

`200 { "ok": true }` + `Set-Cookie: portal.session`.

Lỗi: `400` email sai, `403` dev login tắt.

### `GET /api/auth/csrf`

`200 { "token": "..." }` + cookie `portal.csrf`. Token cũng được set cookie để
antiforgery validate cặp cookie/header.

### `POST /api/auth/logout`

Cần đăng nhập + CSRF. `204`. Xoá cookie session.

## 2. Người dùng hiện tại

### `GET /api/me`

```json
{
  "id": "77097ea6-3815-4b07-a91f-9660ae8c667c",
  "email": "admin@example.com",
  "name": "Portal Admin",
  "avatarUrl": null,
  "roles": ["admin"]
}
```

`401` nếu chưa đăng nhập.

### `GET /api/me/apps`

Danh sách app user được mở.

```json
[
  {
    "id": "6bc3e734-...",
    "slug": "demo",
    "name": "Demo App",
    "description": "Ứng dụng mẫu ...",
    "iconUrl": null,
    "baseUrl": "http://localhost:5174",
    "status": "Active",
    "sortOrder": 100
  }
]
```

Lọc: `status = Active`; non-admin chỉ app có `app_access` khớp role.
Sắp xếp `sortOrder`, `name`.

### `POST /api/apps/{slug}/launch`

Cần đăng nhập + CSRF. Rate limit **30/phút/IP**.

`200`:

```json
{
  "redirectUrl": "http://localhost:5174/auth/callback?code=XXXX",
  "expiresInSeconds": 60
}
```

`404` nếu app không tồn tại/không active/không có quyền (cùng một response để
không lộ app có tồn tại hay không).

## 3. Token cho app vệ tinh

### `POST /api/auth/token`

Anonymous. Rate limit **60/phút/IP**.

```json
{
  "clientId": "demo",
  "clientSecret": "demo-secret-change-me",
  "code": "XXXX"
}
```

`200`:

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "user": { "id": "...", "email": "...", "name": "...", "avatarUrl": null },
  "roles": ["member"]
}
```

`400 { "error": "invalid_grant" }` cho mọi lỗi (client sai, code sai, code dùng rồi,
code hết hạn, code của app khác).

### `GET /api/.well-known/jwks.json`

```json
{
  "keys": [
    { "kty": "RSA", "use": "sig", "alg": "RS256", "kid": "0fdKfUgRzNzJ8SuP", "n": "...", "e": "AQAB" }
  ]
}
```

## 4. Admin

Tất cả yêu cầu role `admin` (`403` nếu không) + CSRF cho request ghi.

### `GET /api/admin/apps`

Mảng `AdminApp`:

```json
{
  "id": "...",
  "slug": "demo",
  "name": "Demo App",
  "description": "...",
  "iconUrl": null,
  "baseUrl": "http://localhost:5174",
  "callbackPath": "/auth/callback",
  "status": "Active",
  "sortOrder": 100,
  "roleIds": ["..."],
  "clientId": "demo"
}
```

### `POST /api/admin/apps`

Tạo app. Body:

```json
{
  "slug": "app1",
  "name": "App 1",
  "description": "Mô tả",
  "iconUrl": null,
  "baseUrl": "https://app1.example.com",
  "callbackPath": "/auth/callback",
  "status": "Active",
  "sortOrder": 10,
  "roleIds": ["<role-guid>"]
}
```

`201`:

```json
{
  "app": { "...AdminApp..." },
  "clientSecret": "plaintext-chi-hien-mot-lan"
}
```

Lỗi: `400` validate, `409` slug trùng.

### `PUT /api/admin/apps/{id}`

Cập nhật; cùng schema body. `200 AdminApp`. `404` nếu không có.

### `DELETE /api/admin/apps/{id}`

`204`. Cascade xoá `app_access`, `app_clients`, `launch_codes` của app.

### `POST /api/admin/apps/{id}/client/rotate`

Sinh secret mới. `200`:

```json
{ "clientId": "app1", "clientSecret": "plaintext-chi-hien-mot-lan" }
```

Nếu app chưa có client, tạo mới với `clientId = slug`.

### `GET /api/admin/roles`

```json
[{ "id": "...", "name": "admin" }, { "id": "...", "name": "member" }]
```

### `GET /api/admin/users?search=`

Tối đa 200 user, tìm theo email hoặc tên.

```json
[
  {
    "id": "...",
    "email": "member@example.com",
    "name": "Member User",
    "avatarUrl": null,
    "roles": ["member"],
    "createdAt": "2026-11-12T11:22:00+00:00",
    "lastLoginAt": "2026-11-12T11:25:00+00:00"
  }
]
```

### `PUT /api/admin/users/{id}/roles`

```json
{ "roles": ["member", "admin"] }
```

`200 AdminUser`. Lỗi:

- `400` role không tồn tại.
- `400` tự bỏ quyền admin của chính mình.
- `400` bỏ admin của admin cuối cùng.

### `GET /api/admin/audit?take=100`

`take` clamp 1..200, sắp xếp mới nhất trước.

```json
[
  {
    "id": 12,
    "userId": "...",
    "appId": "...",
    "action": "app.launch",
    "ipAddress": "127.0.0.1",
    "createdAt": "2026-11-12T11:30:00+00:00"
  }
]
```

## 5. Health

### `GET /api/health`

`200 Healthy` khi API và DB sẵn sàng.

## 6. Mã lỗi

| Mã | Ý nghĩa |
| --- | --- |
| `400` | validate, CSRF sai, `invalid_grant` |
| `401` | chưa đăng nhập (API trả JSON, không redirect) |
| `403` | thiếu role (vd. không phải admin), dev login tắt |
| `404` | không tồn tại hoặc không có quyền |
| `409` | slug trùng |
| `429` | vượt rate limit |
| `503` | Google OAuth chưa cấu hình |

Response lỗi dùng `ProblemDetails` (`title`, `detail`, `status`) hoặc `{ "error": "..." }`
tuỳ endpoint, thống nhất theo từng nhóm.
