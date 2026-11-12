# Portal — hệ sinh thái ứng dụng

Portal là **IdP nhẹ + app launcher**: người dùng đăng nhập một lần bằng Google, sau đó chỉ thấy những ứng dụng mà role của họ được phép truy cập. Các app vệ tinh chạy ở subdomain riêng và nhận danh tính qua **launch code dùng một lần → JWT**.

```
Người dùng ──▶ portal.domain.com (React) ──▶ /api (ASP.NET Core)
                    │                              │
                    │  1. Google login             │  PostgreSQL
                    │  2. danh sách app theo role   │
                    │  3. launch code (60s)         │
                    ▼                              ▼
             app1.domain.com  ◀── JWT 15 phút ── portal.domain.com/api/auth/token
```

## Tính năng hiện có

- Đăng nhập Google (OAuth 2.0) + cookie session HttpOnly.
- Dev login cho môi trường phát triển.
- Danh sách app theo **role → app** (admin thấy tất cả app đang bật).
- Launch code dùng một lần, đổi lấy JWT RS256; công bố JWKS để app tự xác minh.
- Admin UI: CRUD app, gán role, quản lý role người dùng, rotate client secret, nhật ký hoạt động.
- CSRF protection, rate limit, audit log, i18n vi/en.
- Demo app để kiểm chứng SSO end-to-end.

## Cấu trúc repo

| Thư mục | Nội dung |
| --- | --- |
| `backend/` | .NET 10: Domain, Application, Infrastructure, Api, Tests |
| `frontend/` | React 19 + Vite + TypeScript + Tailwind, i18n vi/en |
| `demo-app/` | Node thuần, minh hoạ app vệ tinh đổi code lấy JWT và verify JWKS |
| `deploy/` | Docker Compose + Caddy + `.env.example` |
| `docs/` | Kiến trúc, luồng auth, API, data model, vận hành, tích hợp app |
| `AGENTS.md` | Ngữ cảnh và quy ước cho AI agent |

## Chạy local

Yêu cầu: .NET SDK 10, Node 24 + pnpm, PostgreSQL 16+.

```bash
# 1. Database (mặc định: portal/portal@localhost:5432/portal)
createdb -U postgres portal   # hoặc dùng Docker bên dưới

# 2. API — http://localhost:5080
cd backend
dotnet run --project src/Portal.Api

# 3. Frontend — http://localhost:5173
cd frontend
pnpm install
pnpm dev

# 4. Demo app — http://localhost:5174
cd demo-app
node server.mjs
```

Mở http://localhost:5173, chọn **đăng nhập dev** với `admin@example.com` (mật khẩu không cần vì đây là dev login).

### Chạy toàn bộ bằng Docker

```bash
cd deploy
cp .env.example .env        # sửa domain, secret, Google client
docker compose up -d --build
```

Chi tiết xem [docs/deployment.md](docs/deployment.md).

## Cấu hình Google OAuth

1. Vào Google Cloud Console → APIs & Services → Credentials → **Create OAuth client ID** (Web application).
2. Authorized redirect URI:
   - Dev: `http://localhost:5080/api/auth/google/callback`
   - Production: `https://portal.example.com/api/auth/google/callback`
3. Điền `Auth:Google:ClientId` và `Auth:Google:ClientSecret` (user-secrets hoặc biến môi trường `Auth__Google__ClientId`, `Auth__Google__ClientSecret`).

Nếu chưa cấu hình Google, nút Google trả 503; dev login vẫn dùng được khi `Auth:AllowDevLogin=true`.

## Seed mặc định

| Dữ liệu | Giá trị |
| --- | --- |
| Role | `admin`, `member` |
| Admin | `admin@example.com` (đổi qua `Seed:AdminEmail`) |
| Demo app | slug `demo`, URL `http://localhost:5174`, client id `demo` |
| Demo client secret | `demo-secret-change-me` (đổi qua `Seed:DemoAppClientSecret`) |

## Lệnh thường dùng

```bash
# Backend
dotnet build backend/Portal.slnx
dotnet test backend/Portal.slnx
dotnet ef migrations add <Name> -p backend/src/Portal.Infrastructure -s backend/src/Portal.Api -o Persistence/Migrations

# Frontend
cd frontend && pnpm typecheck && pnpm build

# Đổi mật khẩu DB / kiểm tra sức khoẻ
curl http://localhost:5080/api/health
```

## Tài liệu

- [Kiến trúc](docs/architecture.md)
- [Luồng xác thực](docs/auth-flow.md)
- [Data model](docs/data-model.md)
- [API](docs/api.md)
- [Phát triển](docs/development.md)
- [Triển khai](docs/deployment.md)
- [Tích hợp app mới](docs/app-integration.md)
- [Bảo mật](docs/security.md)
- [ADR-0001: chiến lược auth](docs/decisions/ADR-0001-auth-strategy.md)

## Giấy phép

Nội bộ — chưa chọn giấy phép mã nguồn mở.
