# Phát triển

## 1. Yêu cầu môi trường

| Công cụ | Phiên bản | Ghi chú |
| --- | --- | --- |
| .NET SDK | 10.x | `dotnet --version` |
| Node.js | 24.x (LTS) | `node -v` |
| pnpm | 12.x | `corepack enable` rồi `corepack prepare pnpm@12.18.0 --activate` |
| PostgreSQL | 16+ | local hoặc Docker |
| Docker (tuỳ chọn) | 24+ | để test compose/Caddy |

Kiểm tra nhanh:

```bash
dotnet --list-sdks
node -v && pnpm -v
psql --version
```

## 2. Cài đặt lần đầu

```bash
git clone <repo> portal && cd portal

# Database
createdb -U postgres portal
psql -U postgres -c "CREATE ROLE portal LOGIN PASSWORD 'portal';"
psql -U postgres -c "ALTER DATABASE portal OWNER TO portal;"
```

Hoặc dùng Docker chỉ cho Postgres:

```bash
docker run -d --name portal-pg \
  -e POSTGRES_DB=portal -e POSTGRES_USER=portal -e POSTGRES_PASSWORD=portal \
  -p 5432:5432 postgres:17-alpine
```

Connection string mặc định nằm trong `backend/src/Portal.Api/appsettings.json`:
`Host=localhost;Port=5432;Database=portal;Username=portal;Password=portal`.

Có thể override bằng biến môi trường `ConnectionStrings__Postgres`.

## 3. Chạy dev

Mở các terminal (hoặc chạy nền):

```bash
# 1) API — http://localhost:5080
cd backend
dotnet run --project src/Portal.Api

# 2) Frontend — http://localhost:5173 (proxy /api → 5080)
cd frontend
pnpm install
pnpm dev

# 3) Demo app Node — http://localhost:5174
cd demo-app
node server.mjs

# 4) Taskboard (.NET + React) — http://localhost:5181
cd demo-apps/tasks/api
dotnet run

# 5) Team Notes (.NET + React) — http://localhost:5182
cd demo-apps/notes/api
dotnet run
```

Mở http://localhost:5173 → đăng nhập `admin@example.com` / `ChangeMe_123456`, hoặc dùng
**Đăng nhập dev** với `admin@example.com`.

`appsettings.Development.json` bật `Auth:AllowDevLogin=true` và seed mật khẩu admin
(`Seed:AdminPassword`). Startup tự migrate DB và seed: role `admin`/`member`, user admin,
app `demo` + client `demo`.

### Biến môi trường hay dùng

| Biến | Ý nghĩa |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` để bật dev login |
| `ASPNETCORE_URLS` | đổi cổng API |
| `ConnectionStrings__Postgres` | chuỗi kết nối |
| `Auth__Google__ClientId` / `__ClientSecret` | bật Google login |
| `Auth__AllowDevLogin` | bật/tắt dev login |
| `Auth__FrontendUrl` | URL SPA để redirect sau login |
| `Jwt__Issuer` | `iss` trong JWT, phải là URL công khai của portal |
| `Jwt__PrivateKeyPath` / `Jwt__PrivateKeyPem` | khoá ký RS256 |
| `Security__SecretPepper` | pepper hash secret/code |
| `Seed__AdminEmail` | email admin seed |
| `Seed__AdminPassword` | mật khẩu đăng nhập cục bộ của admin seed (≥ 12 ký tự) |
| `Cors__AllowedOrigins__0` | origin được phép (khi tách domain) |

Dùng user-secrets cho bí mật local:

```bash
cd backend/src/Portal.Api
dotnet user-secrets init
dotnet user-secrets set "Auth:Google:ClientId" "..."
dotnet user-secrets set "Auth:Google:ClientSecret" "..."
dotnet user-secrets set "Security:SecretPepper" "..."
```

## 4. Kiểm thử

```bash
# Backend
cd backend
dotnet build
dotnet test

# Frontend
cd frontend
pnpm typecheck
pnpm build
```

Test hiện có:

- `SecretHasherTests`: hash ổn định, không chứa plaintext, verify đúng/sai.
- `JwtTokenIssuerTests`: JWT RS256 hợp lệ + claim đúng, từ chối token bị sửa,
  JWKS có `kty/use/alg/kid/n/e`.

### Kiểm tra nhanh bằng curl

```bash
API=http://localhost:5080
JAR=/tmp/portal-cookies.txt

# đăng nhập bằng mật khẩu (cần CSRF)
CSRF=$(curl -s -c $JAR -b $JAR $API/api/auth/csrf | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])')
curl -s -c $JAR -b $JAR -X POST $API/api/auth/password-login \
  -H 'Content-Type: application/json' -H "X-CSRF-TOKEN: $CSRF" \
  -d '{"email":"admin@example.com","password":"ChangeMe_123456"}'

# hoặc dev login
curl -s -c $JAR -X POST $API/api/auth/dev-login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@example.com"}'

# dùng session
curl -s -b $JAR $API/api/me

# launch (cần CSRF)
CSRF=$(curl -s -c $JAR -b $JAR $API/api/auth/csrf | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])')
curl -s -b $JAR -X POST $API/apps/demo/launch -H "X-CSRF-TOKEN: $CSRF"
```

Đổi `code` từ `redirectUrl` thành JWT:

```bash
curl -s -X POST $API/api/auth/token \
  -H 'Content-Type: application/json' \
  -d '{"clientId":"demo","clientSecret":"demo-secret-change-me","code":"<code>"}'
```

## 5. Migration

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
export DOTNET_ROOT=/usr/local/share/dotnet   # chỉ khi SDK không ở /usr/share/dotnet

cd backend
dotnet ef migrations add <Ten> \
  -p src/Portal.Infrastructure -s src/Portal.Api -o Persistence/Migrations
dotnet ef database update -p src/Portal.Infrastructure -s src/Portal.Api
```

Quy ước:

- Mỗi thay đổi model đi kèm migration, không sửa migration đã merge.
- Không dùng `EnsureCreated()`.
- API tự migrate khi khởi động; production nên chạy migration riêng trước khi rollout.

## 6. Frontend

- React 19, Vite 7, TypeScript strict, Tailwind 4 (`@tailwindcss/vite`, không cần `tailwind.config`).
- Server state: TanStack Query. Query key đang dùng: `['me']`, `['me','apps']`,
  `['admin','apps']`, `['admin','roles']`, `['admin','users',search]`, `['admin','audit']`.
- Mọi `fetch` đi qua `src/lib/api.ts`; client tự gắn `X-CSRF-TOKEN` cho request ghi
  và retry một lần khi CSRF hết hạn.
- i18n: thêm key vào **cả** `src/locales/vi.ts` và `src/locales/en.ts`;
  mặc định `vi`, lưu lựa chọn ở `localStorage.portal.lang`.
- Style: dùng component trong `src/components/ui.tsx` (`Button`, `Card`, `Field`,
  `Badge`, `Spinner`, banner) để giữ giao diện đồng nhất.

### pnpm và build script

pnpm 12 chặn build script mặc định. Repo cấu hình ở `frontend/pnpm-workspace.yaml`:

```yaml
onlyBuiltDependencies:
  - esbuild
```

Nếu vẫn gặp `ERR_PNPM_IGNORED_BUILDS`, chạy `pnpm install --allow-build=esbuild`.

## 7. Demo app

`demo-app/server.mjs` là Node HTTP thuần, không dependency:

- `GET /` — hướng dẫn.
- `GET /auth/callback?code=` — đổi code lấy JWT, verify RS256 bằng JWKS, hiển thị user.
- `GET /health` — `ok`.

Chạy: `cd demo-app && node server.mjs`. Đổi cổng/URL bằng `PORT`, `PORTAL_URL`,
`CLIENT_ID`, `CLIENT_SECRET`.

### 7.1. Taskboard và Team Notes (.NET + React)

Hai app đầy đủ trong `demo-apps/`, minh hoạ cùng một mẫu tích hợp:

- Backend ASP.NET Core minimal API, cookie session riêng, antiforgery cho mutation.
- Đổi launch code và verify JWT qua thư viện dùng chung `demo-apps/shared/PortalSso`.
- Frontend React 19 + Vite + Tailwind 4, i18n vi/en, TanStack Query.
- Bundle frontend được build vào `api/wwwroot`, nên chỉ cần chạy backend.

| App | Backend | Vite dev | Client id | Seed secret |
| --- | --- | --- | --- | --- |
| Taskboard | http://localhost:5181 | http://localhost:5183 | `tasks` | `tasks-secret-change-me` |
| Team Notes | http://localhost:5182 | http://localhost:5184 | `notes` | `notes-secret-change-me` |

```bash
# Chạy backend (phục vụ luôn bundle trong wwwroot)
cd demo-apps/tasks/api && dotnet run
cd demo-apps/notes/api && dotnet run

# Dev giao diện có hot reload (proxy /api và /auth về backend)
cd demo-apps/tasks/web && pnpm install && pnpm dev
cd demo-apps/notes/web && pnpm install && pnpm dev

# Build lại bundle sau khi sửa UI
cd demo-apps/tasks/web && pnpm build
cd demo-apps/notes/web && pnpm build
```

Portal seed sẵn hai app này khi `Seed:DemoData=true` (mặc định ở dev) và gán cho role
`member`; `admin` thấy mọi app. URL và secret đổi qua `Seed:TasksAppUrl`,
`Seed:TasksClientSecret`, `Seed:NotesAppUrl`, `Seed:NotesClientSecret`.

## 8. Debug thường gặp

| Triệu chứng | Nguyên nhân / cách xử lý |
| --- | --- |
| API 500 khi khởi động, lỗi Npgsql | Postgres chưa chạy hoặc sai connection string |
| `401` ở `GET /api/me` | chưa login hoặc cookie bị xoá; dev login lại |
| `400` khi POST | thiếu `X-CSRF-TOKEN`; gọi `/api/auth/csrf` trước |
| Google trả `redirect_uri_mismatch` | redirect URI trong Google Console phải là `.../api/auth/google/callback` |
| App nhận `invalid_grant` | code hết hạn (60s), đã dùng, hoặc client secret sai |
| JWT `iss` sai | đặt `Jwt__Issuer` bằng URL công khai của portal |
| Demo app báo “Chữ ký JWT không hợp lệ” | JWKS cache cũ sau khi đổi khoá; restart demo app |
| `pnpm` báo ignored build `esbuild` | `pnpm install --allow-build=esbuild` |
