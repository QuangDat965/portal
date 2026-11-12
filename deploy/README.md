# Deploy — cấu hình nhanh

Cheat sheet để **dựng & cấu hình** stack bằng Docker Compose với ít thao tác nhất.
Tài liệu vận hành đầy đủ (VPS, backup, giám sát, gỡ lỗi sâu): [../docs/deployment.md](../docs/deployment.md).

```
Internet ─▶ Caddy :80/:443 ─┬─ portal.<domain>   /api/* → api:8080, /* → web:80 (nginx + React)
                            └─ demo.<domain>              → demo:5174 (Node)
                                  api ─▶ postgres:5432
```

---

## 1. Quickstart (3 lệnh)

```bash
cd deploy
cp .env.example .env      # rồi sửa các giá trị BẮT BUỘC bên dưới
docker compose up -d --build
docker compose ps && curl -fsS https://<PORTAL_DOMAIN>/api/health
```

Yêu cầu: Docker Engine + Compose plugin; DNS `A` cho `PORTAL_DOMAIN` và `DEMO_DOMAIN` trỏ về máy chủ; mở port 80/443.

Sinh bí mật nhanh:

```bash
openssl rand -base64 48   # SECRET_PEPPER
openssl rand -base64 32   # POSTGRES_PASSWORD / DEMO_CLIENT_SECRET
```

---

## 2. Toàn bộ biến trong `.env`

> File được `docker compose` tự đọc (không cần khai báo `env_file`). Cột **Biến compose** cho biết giá trị được đổ vào đâu.

### Bắt buộc đổi trước khi lên production

| Biến `.env` | Ý nghĩa | Được dùng ở | Ghi chú |
| --- | --- | --- | --- |
| `PORTAL_DOMAIN` | Domain portal | Caddy | vd. `portal.example.com` |
| `DEMO_DOMAIN` | Domain demo | Caddy | vd. `demo.example.com` |
| `TASKS_DOMAIN` | Domain Taskboard | Caddy | vd. `tasks.example.com` |
| `NOTES_DOMAIN` | Domain Team Notes | Caddy | vd. `notes.example.com` |
| `KITTENS_DOMAIN` | Domain Mèo Nổ | Caddy | vd. `kittens.example.com` |
| `PUBLIC_KITTENS_URL` | URL đầy đủ Mèo Nổ | `Seed__KittensAppUrl` | Phải khớp KITTENS_DOMAIN |
| `KITTENS_CLIENT_SECRET` | Secret client `exploding-kittens` | Seed và service kittens | Bắt buộc đổi ở production |
| `PUBLIC_PORTAL_URL` | URL đầy đủ portal (`https://…`) | `Auth__FrontendUrl`, `Jwt__Issuer`, `Cors__AllowedOrigins__0`, `Portal__Url`/`Portal__Issuer` của tasks & notes | **Phải khớp scheme+host** với `PORTAL_DOMAIN` |
| `PUBLIC_DEMO_URL` | URL đầy đủ demo | `Seed__DemoAppUrl` | |
| `PUBLIC_TASKS_URL` | URL đầy đủ Taskboard | `Seed__TasksAppUrl` | khớp `TASKS_DOMAIN` |
| `PUBLIC_NOTES_URL` | URL đầy đủ Team Notes | `Seed__NotesAppUrl` | khớp `NOTES_DOMAIN` |
| `ACME_EMAIL` | Email Let's Encrypt | Caddy | |
| `POSTGRES_PASSWORD` | Mật khẩu DB | `ConnectionStrings__Postgres` | Mạnh, không dùng mặc định |
| `SECRET_PEPPER` | Pepper cho HMAC secret | `Security__SecretPepper` | ≥ 32 ký tự, **không đổi sau khi có dữ liệu** |
| `ALLOW_DEV_LOGIN` | Đăng nhập dev | `Auth__AllowDevLogin` | **`false`** ở production |

### Google OAuth (tùy chọn — bỏ trống vẫn đăng nhập mật khẩu/dev được)

| Biến `.env` | Được dùng ở | Ghi chú |
| --- | --- | --- |
| `GOOGLE_CLIENT_ID` | `Auth__Google__ClientId` | |
| `GOOGLE_CLIENT_SECRET` | `Auth__Google__ClientSecret` | redirect URI: `https://<PORTAL_DOMAIN>/api/auth/google/callback` |

### Database

| Biến `.env` | Mặc định | Được dùng ở |
| --- | --- | --- |
| `POSTGRES_DB` | `portal` | postgres + connection string |
| `POSTGRES_USER` | `portal` | postgres + connection string |
| `POSTGRES_PASSWORD` | `portal` | postgres + connection string |

### Seed & demo

| Biến `.env` | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `SEED_ADMIN_EMAIL` | `admin@example.com` | Email admin đầu tiên |
| `SEED_ADMIN_NAME` | `Portal Admin` | Tên hiển thị |
| `SEED_ADMIN_PASSWORD` | (trống) | Mật khẩu cục bộ admin (≥ 12 ký tự). Trống nếu chỉ dùng Google |
| `SEED_DEMO_DATA` | `true` | Tạo app demo + client. Đặt `false` khi production nếu không cần |
| `DEMO_CLIENT_SECRET` | `demo-secret-change-me` | Secret của client `demo` |
| `TASKS_CLIENT_SECRET` | `tasks-secret-change-me` | Secret của client `tasks` (cũng là `Portal__ClientSecret` của service `tasks`) |
| `NOTES_CLIENT_SECRET` | `notes-secret-change-me` | Secret của client `notes` (cũng là `Portal__ClientSecret` của service `notes`) |

### Biến chỉ đặt trong `docker-compose.yml` (không cần khai báo ở `.env`)

| Biến | Được set bởi compose | Ý nghĩa |
| --- | --- | --- |
| `Jwt__PrivateKeyPath` | `/data/keys/jwt-private.pem` | RSA key, giữ nhờ volume `keys` |
| `DataProtection__KeysPath` | `/data/keys` | DataProtection keys (persist để không logout sau deploy) |

> Nếu cần cấp JWT key riêng: thêm `Jwt__PrivateKeyPem=<PEM>` vào service `api` (hoặc mount file vào `/data/keys`). **Không commit key.**

---

## 3. Bảng domain → service

| Domain | Route | Service |
| --- | --- | --- |
| `PORTAL_DOMAIN` | `/api/*` | `api:8080` |
| `PORTAL_DOMAIN` | `/*` | `web:80` (nginx, React SPA) |
| `DEMO_DOMAIN` | `/*` | `demo:5174` (Node) |
| `TASKS_DOMAIN` | `/*` | `tasks:8080` (.NET + React SPA) |
| `NOTES_DOMAIN` | `/*` | `notes:8080` (.NET + React SPA) |
| `KITTENS_DOMAIN` | `/*` | `kittens:8080` (.NET + React SPA) |

Tất cả service đã có sẵn trong `docker-compose.yml` và `Caddyfile` — chỉ cần khai domain trong `.env` là xong. Thêm app khác → xem mục 5.

---

## 4. Lệnh vận hành thường dùng

```bash
cd deploy
docker compose up -d --build          # dựng / cập nhật
docker compose ps                     # trạng thái
docker compose logs -f api            # log API
docker compose restart api            # restart 1 service
docker compose down                   # dừng (giữ volume)
docker compose down -v                # XOÁ volume (mất dữ liệu!)

# DB
docker compose exec postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"
docker compose exec -T postgres pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" | gzip > backup-$(date +%F).sql.gz

# Health
curl -fsS https://<PORTAL_DOMAIN>/api/health    # API + DB
curl -fsS https://<DEMO_DOMAIN>/health          # demo
curl -fsS https://<TASKS_DOMAIN>/api/health     # tasks
curl -fsS https://<NOTES_DOMAIN>/api/health     # notes
```

Cập nhật phiên bản: `git pull && docker compose up -d --build` (migration tự chạy khi API khởi động; volume `pgdata`/`keys`/`caddy_data` giữ nguyên dữ liệu).

---

## 5. Apps có sẵn & thêm app mới

### Đã tích hợp sẵn (không cần làm gì)

`demo`, `tasks`, `notes` đã có service trong `docker-compose.yml` + block trong `Caddyfile` + Dockerfile riêng:

| App | Dockerfile | Build context |
| --- | --- | --- |
| demo (Node) | `demo-app/Dockerfile` | `../demo-app` |
| tasks (.NET+React) | `demo-apps/tasks/api/Dockerfile` | `../demo-apps` (`dockerfile: tasks/api/Dockerfile`) |
| notes (.NET+React) | `demo-apps/notes/api/Dockerfile` | `../demo-apps` (`dockerfile: notes/api/Dockerfile`) |

Chỉ cần đặt `TASKS_DOMAIN` / `NOTES_DOMAIN` / `PUBLIC_TASKS_URL` / `PUBLIC_NOTES_URL` / `*_CLIENT_SECRET` trong `.env`.

> **Lưu ý build SPA**: Dockerfile của tasks/notes **không** build React (tránh kéo Node vào image). Bundle phải có sẵn trong `api/wwwroot` — repo đã commit sẵn. Khi sửa UI, build lại trước khi `docker compose build`:
>
> ```bash
> cd demo-apps/tasks/web && pnpm install && pnpm build   # -> api/wwwroot
> cd demo-apps/notes/web && pnpm install && pnpm build
> ```

### Thêm app vệ tinh mới (vd. `app1`)

1. DNS: trỏ `app1.<domain>` về máy chủ.
2. `Caddyfile`:
   ```
   {$APP1_DOMAIN} {
       encode zstd gzip
       reverse_proxy app1:8080
   }
   ```
   và thêm `APP1_DOMAIN` vào env của service `caddy` (`APP1_DOMAIN: ${APP1_DOMAIN:-app1.localhost}`).
3. Thêm service `app1` vào `docker-compose.yml`:
   ```yaml
   app1:
     build: { context: ../app1, dockerfile: Dockerfile }
     restart: unless-stopped
     environment:
       ASPNETCORE_ENVIRONMENT: Production
       ASPNETCORE_URLS: http://+:8080
       Portal__Url: ${PUBLIC_PORTAL_URL:-https://portal.localhost}
       Portal__Issuer: ${PUBLIC_PORTAL_URL:-https://portal.localhost}
       Portal__FrontendUrl: ${PUBLIC_PORTAL_URL:-https://portal.localhost}
       Portal__ClientId: app1
       Portal__ClientSecret: ${APP1_CLIENT_SECRET:-change-me}
       Cookie__Secure: "true"
     depends_on: [caddy]
   ```
4. `.env`: thêm `APP1_DOMAIN`, `PUBLIC_APP1_URL`, `APP1_CLIENT_SECRET`.
5. Admin UI → **Apps** → tạo app `slug=app1`, `BaseUrl=https://app1.<domain>`, `CallbackPath=/auth/callback`, gán role.
6. `docker compose up -d --build`.

> App chỉ dùng 2 endpoint công khai: `POST /api/auth/token` và `GET /.well-known/jwks.json` (dưới `PUBLIC_PORTAL_URL`). Xem [../docs/app-integration.md](../docs/app-integration.md).

---

## 6. Checklist trước khi lên production

- [ ] `ALLOW_DEV_LOGIN=false`
- [ ] `SECRET_PEPPER` mạnh + đã backup an toàn
- [ ] `POSTGRES_PASSWORD` mạnh
- [ ] `PUBLIC_PORTAL_URL` = `https://` + đúng domain, khớp `PORTAL_DOMAIN`
- [ ] Google redirect URI đúng domain production (nếu dùng Google)
- [ ] `SEED_DEMO_DATA=false` hoặc đã rotate `DEMO_CLIENT_SECRET`
- [ ] TLS hoạt động, HSTS bật (Caddyfile đã có)
- [ ] Backup định kỳ `pgdata` + `keys`
- [ ] Log không chứa secret/token/cookie
- [ ] Đã thử end-to-end: đăng nhập → mở app → nhận JWT trên domain thật

---

## 7. Lỗi thường gặp

| Triệu chứng | Nguyên nhân / xử lý |
| --- | --- |
| Caddy không lấy được cert | DNS chưa trỏ đúng, port 80/443 bị chặn |
| API lỗi kết nối DB | `postgres` chưa healthy, sai `POSTGRES_*` |
| `502` từ Caddy | `api`/`web` chưa lên — `docker compose ps`, xem log |
| Đăng nhập Google xong về lại login | Sai `PUBLIC_PORTAL_URL`/`Jwt__Issuer`, hoặc cookie Secure/SameSite |
| App báo invalid signature | JWKS cache cũ / JWT key bị sinh lại (volume `keys` mất) |
| Mọi user bị logout sau deploy | `DataProtection__KeysPath` không persist |
| Launch app ra `localhost` | `BaseUrl` của app trong Admin UI vẫn là localhost — sửa thành domain thật |

Mèo Nổ: service `kittens` tự build cả React và .NET từ `demo-apps/`.
State lưu trong schema `exploding_kittens` cùng PostgreSQL hiện có; cookie keys lưu
ở volume `kittens_keys`. Xem [cấu hình và luật MVP](../docs/exploding-kittens.md).
