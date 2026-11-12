# AGENTS.md

Tài liệu này là **nguồn sự thật duy nhất** cho mọi AI agent làm việc trong repo.
Đọc kỹ trước khi sửa code. Nếu thay đổi hành vi hệ thống, cập nhật lại file này
và tài liệu liên quan.

## 1. Bối cảnh

Portal là một **IdP nhẹ + app launcher** cho hệ sinh thái ứng dụng nội bộ.

- Người dùng đăng nhập một lần bằng Google hoặc email/mật khẩu cục bộ tại `portal.domain.com`.
- Portal trả danh sách app mà user được phép dùng (quyền theo **role → app**).
- Khi mở app, portal phát **launch code dùng một lần (60s)**.
- App backend đổi code tại `POST /api/auth/token` bằng `client_id` + `client_secret`
  để nhận **JWT RS256 (15 phút)**, và xác minh chữ ký qua JWKS.
- Các app vệ tinh chạy ở subdomain riêng; không dùng chung cookie với portal.

## 2. Cấu trúc repo

```
backend/
  src/Portal.Domain          entity thuần, không phụ thuộc hạ tầng
  src/Portal.Application     DTO + interface (abstractions)
  src/Portal.Infrastructure  EF Core, Identity, JWT, service implementations
  src/Portal.Api             endpoint, auth setup, DI, cấu hình
  tests/Portal.Tests         unit test (xUnit)
frontend/
  src/pages                  Login, Launcher, Profile, Admin
  src/components             UI dùng lại
  src/lib                    api client, i18n, format
  src/locales                vi.ts, en.ts
demo-app/                    app mẫu Node thuần, verify JWT bằng JWKS
demo-apps/                    ba app .NET + React (tasks, notes, exploding-kittens) + PortalSso dùng chung
deploy/                      docker-compose, Caddyfile, .env.example
docs/                        tài liệu kiến trúc/vận hành
```

## 3. Lệnh chuẩn

```bash
# Backend
cd backend
dotnet build
dotnet test
dotnet ef migrations add <Name> -p src/Portal.Infrastructure -s src/Portal.Api -o Persistence/Migrations

# Frontend
cd frontend
pnpm install
pnpm typecheck
pnpm build

# Toàn hệ thống
cd deploy && docker compose up -d --build
```

## 4. Quy ước code

### Backend

- .NET 10, nullable enabled, async toàn bộ I/O.
- Endpoint dùng minimal API; **không** đặt business logic trong endpoint — đưa vào
  `Portal.Infrastructure/Services` hoặc `Portal.Application`.
- Dùng `DateTimeOffset.UtcNow` cho thời gian nghiệp vụ.
- Mọi thay đổi schema phải đi kèm EF migration.
- Response lỗi dùng `Results.Problem`/`{ error }` thống nhất; không trả stack trace.
- Không log token, secret, launch code, cookie.

### Frontend

- Functional component + TypeScript strict; không dùng `any` nếu tránh được.
- Data fetching qua TanStack Query; không gọi `fetch` rải rác ngoài `src/lib/api.ts`.
- Mọi chữ hiển thị phải đi qua `t()`; thêm key vào **cả** `vi.ts` và `en.ts`.
- Mutation dùng cookie phải đi qua `api` client để tự gắn CSRF header.
- Không hard-code URL API; dùng đường dẫn tương đối `/api/...` (dev proxy + Caddy).

## 5. Luật bảo mật

- Không tin `app_id`, `user_id`, `role` do client gửi lên; luôn suy ra từ session/JWT.
- Kiểm tra entitlement ở server trước khi phát launch code.
- Launch code: hash khi lưu, TTL 60s, dùng một lần (atomic update).
- Client secret: chỉ lưu hash HMAC-SHA256 có pepper; chỉ trả plaintext một lần khi tạo/rotate.
- Cookie: HttpOnly, Secure ở production, SameSite=Lax.
- Mật khẩu cục bộ: do ASP.NET Core Identity hash; tối thiểu 12 ký tự; khoá tạm
  5 phút sau 5 lần sai; chỉ admin được đặt/đổi mật khẩu; rate limit `login` 10/phút/IP.
- Mutation dùng cookie auth phải validate antiforgery.
- Không commit `.env`, `keys/`, `*.pem`.

## 6. Definition of Done

Một thay đổi chỉ hoàn thành khi:

1. `dotnet build` và `dotnet test` pass.
2. Nếu đổi schema: có migration mới và đã thử `dotnet ef database update`.
3. `pnpm typecheck` và `pnpm build` pass (nếu đụng frontend).
4. i18n vi/en đầy đủ (nếu thêm text UI).
5. Cập nhật docs/AGENTS.md nếu đổi hành vi, API, cấu hình hoặc luồng auth.
6. Không có secret/token bị log hoặc commit.

## 7. Điều cấm

- Không đổi luồng auth, claim JWT, hoặc mô hình quyền mà không ghi ADR mới trong
  `docs/decisions/`.
- Không thêm package nặng (ORM khác, auth provider khác) khi chưa thống nhất.
- Không bật `Auth:AllowDevLogin` ở production.
- Không dùng `EnsureCreated()` thay migration.
- Không xoá hoặc ghi đè dữ liệu người dùng khi chưa có yêu cầu rõ ràng.

## 8. Điểm vào nhanh

- Auth Google: `backend/src/Portal.Api/Auth/AuthSetup.cs`
- Đăng nhập mật khẩu: `backend/src/Portal.Api/Endpoints/AuthEndpoints.cs` (`/password-login`)
- Endpoint: `backend/src/Portal.Api/Endpoints/`
- Launch + token: `backend/src/Portal.Infrastructure/Services/LaunchService.cs`
- Quyền app: `backend/src/Portal.Infrastructure/Services/EntitlementService.cs`
- EF model: `backend/src/Portal.Infrastructure/Persistence/PortalDbContext.cs`
- Seed: `backend/src/Portal.Infrastructure/Persistence/DbInitializer.cs`
- API client frontend: `frontend/src/lib/api.ts`
- Admin UI: `frontend/src/pages/admin/`

## 9. App Mèo Nổ

- `demo-apps/exploding-kittens/`: app vệ tinh dùng PortalSso; không thay đổi luồng auth/claim/quyền.
- API ở cổng 5185, Vite ở 5186; client id `exploding-kittens`, callback `/auth/callback`.
- Seed khi `Seed:DemoData=true`: role member được truy cập như các app demo khác.
- Ván 2–5 người, state và bài ẩn do server quyết định; schema PostgreSQL `exploding_kittens`
  và bảng migration riêng. Mutation theo phòng dùng PostgreSQL advisory transaction lock.
- MVP: tự động Giải cứu + chèn mèo ngẫu nhiên; Favor lấy bài ngẫu nhiên; chưa có Nope.
  Skip trả một lượt rút; Attack cộng dồn phần nợ còn lại + 2 (bình thường là 2).
- UI dùng TanStack Query cập nhật mỗi 2 giây; không trả bộ bài/tay bài đối thủ qua API/hub.
  Xem tương lai chỉ trả qua response riêng của người đánh. Hub `RoomChanged` chỉ báo room id.
- Kiểm tra: `dotnet test demo-apps/exploding-kittens/tests`; frontend `pnpm typecheck`, `pnpm build`.
  Set `EK_TEST_POSTGRES` tới database kiểm thử riêng để chạy thêm test persistence/concurrency.
- Vận hành/API/luật chi tiết: `docs/exploding-kittens.md`.

- Giao diện Mèo Nổ dùng Motion 14 (`motion/react`), CSS 2.5D và artwork SVG gốc;
  skill Motion tại `demo-apps/exploding-kittens/web/.agents/skills/motion/SKILL.md`.
  Giữ `MotionConfig reducedMotion="user"`, không gắn logic game với thời lượng animation.
- `GameView` bổ sung `revision`, `recentEvents` (32 sự kiện công khai), `discardTop`,
  `discardCount`; migration `PublicGameEvents` không sửa luật hay dữ liệu bài cũ.
  Không đưa loại bài rút, lá Favor lấy được hoặc bài tương lai vào event công khai.
- Presentation state tách khỏi snapshot server; reducer chống sự kiện trùng/lệch thứ tự,
  giữ identity bài và resync khi reconnect. Chạy thêm `pnpm test` trong web Mèo Nổ.
