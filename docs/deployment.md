# Triển khai

Mục tiêu: chạy toàn bộ stack trên VPS bằng **Docker Compose + Caddy** (TLS tự động).

## 1. Kiến trúc triển khai

```
Internet ──▶ Caddy (80/443)
                │
                ├── portal.example.com   /api/*  ──▶ api:8080  (ASP.NET Core)
                │                        /*      ──▶ web:80    (nginx + React SPA)
                │
                └── demo.example.com              ──▶ demo:5174 (Node demo)

api ──▶ postgres:5432
```

- UI và API **cùng host** → cookie first-party, không cần CORS ở production.
- App vệ tinh (như demo) ở subdomain riêng, xác thực bằng launch code + JWT.

## 2. Chuẩn bị VPS

- Ubuntu 22.04/24.04, 2 vCPU / 2 GB RAM trở lên.
- Docker Engine + Compose plugin.
- DNS: bản ghi `A` cho `portal.example.com` và `demo.example.com` trỏ về IP VPS.
- Mở port 80 và 443.

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER   # logout/login lại
docker compose version
```

## 3. Cấu hình

```bash
cd deploy
cp .env.example .env
```

Điền `.env`:

| Biến | Ghi chú |
| --- | --- |
| `PORTAL_DOMAIN` | vd. `portal.example.com` |
| `DEMO_DOMAIN` | vd. `demo.example.com` |
| `PUBLIC_PORTAL_URL` | `https://portal.example.com` (dùng cho `Jwt:Issuer`, redirect, CORS) |
| `PUBLIC_DEMO_URL` | `https://demo.example.com` |
| `ACME_EMAIL` | email Let's Encrypt |
| `GOOGLE_CLIENT_ID` / `GOOGLE_CLIENT_SECRET` | từ Google Cloud Console |
| `POSTGRES_PASSWORD` | mật khẩu mạnh |
| `SECRET_PEPPER` | chuỗi ngẫu nhiên ≥ 32 ký tự, **không đổi sau khi có dữ liệu** |
| `DEMO_CLIENT_SECRET` | secret của demo client |
| `SEED_ADMIN_EMAIL` | email admin đầu tiên |
| `SEED_ADMIN_PASSWORD` | mật khẩu đăng nhập cục bộ cho admin (≥ 12 ký tự); để trống nếu chỉ dùng Google |
| `ALLOW_DEV_LOGIN` | **false** ở production |

Sinh bí mật:

```bash
openssl rand -base64 48   # SECRET_PEPPER
openssl rand -base64 32   # DEMO_CLIENT_SECRET, POSTGRES_PASSWORD
```

### Google OAuth

Authorized redirect URI: `https://portal.example.com/api/auth/google/callback`.

### Khoá ký JWT

Lần chạy đầu, API tự sinh RSA key tại `/data/keys/jwt-private.pem` trong volume `keys`.
Key này được giữ qua các lần deploy nhờ volume. Có thể cấp key riêng:

```env
Jwt__PrivateKeyPem=<nội dung PEM>
```

hoặc mount file và đặt `Jwt:PrivateKeyPath`. **Không commit key.**

## 4. Khởi chạy

```bash
cd deploy
docker compose up -d --build
docker compose ps
docker compose logs -f api
```

Kiểm tra:

```bash
curl -fsS https://portal.example.com/api/health
curl -fsS https://demo.example.com/health
```

Mở `https://portal.example.com` → đăng nhập Google → thấy app theo quyền.

## 5. Cập nhật phiên bản

```bash
git pull
cd deploy
docker compose up -d --build
```

- Migration tự chạy khi API khởi động.
- Rolling restart không mất dữ liệu (volume `pgdata`, `keys`, `caddy_data`).
- Với nhiều instance API, tách bước `dotnet ef database update` ra khỏi startup để tránh race.

## 6. Backup & khôi phục

```bash
# Backup DB
docker compose exec -T postgres pg_dump -U portal portal | gzip > backup-$(date +%F).sql.gz

# Khôi phục
gunzip -c backup-2026-11-12.sql.gz | docker compose exec -T postgres psql -U portal -d portal
```

Nên backup thêm volume `keys` (chứa JWT private key và DataProtection keys) và
`caddy_data` (certificate) — hoặc chấp nhận sinh lại key/cert.

## 7. Vận hành

```bash
docker compose logs -f               # log toàn stack
docker compose logs -f api           # log API (Serilog JSON/console)
docker compose restart api           # restart API
docker compose exec postgres psql -U portal -d portal   # vào DB
docker compose down                  # dừng (giữ volume)
docker compose down -v               # XOÁ volume — mất dữ liệu, cẩn thận
```

### Healthcheck & giám sát

- `GET /api/health` — API + DB.
- `GET /health` — web (nginx) và demo.
- Có thể thêm Uptime Kuma / healthcheck bên ngoài trỏ vào các endpoint trên.

### Log

API dùng Serilog, ghi ra console (Docker thu gom). Nên cấu hình log driver xoay vòng:

```yaml
# thêm vào service api nếu cần
logging:
  driver: json-file
  options:
    max-size: "10m"
    max-file: "5"
```

**Không log** secret, token, launch code, cookie.

## 8. Thêm app mới (subdomain)

1. Trỏ DNS `app1.example.com` về VPS.
2. Thêm block vào `deploy/Caddyfile`:

   ```
   app1.example.com {
       reverse_proxy app1:3000
   }
   ```

3. Thêm service `app1` vào `docker-compose.yml`, cùng network mặc định.
4. Trong Admin UI → Apps → tạo app với `BaseUrl = https://app1.example.com`,
   `CallbackPath = /auth/callback`, chọn role được truy cập.
5. Lưu `clientSecret` trả về một lần, cấu hình trong app.
6. App verify JWT bằng `https://portal.example.com/api/.well-known/jwks.json`.

Xem [app-integration.md](app-integration.md).

## 9. Checklist trước khi lên production

- [ ] `ALLOW_DEV_LOGIN=false`
- [ ] `SECRET_PEPPER` mạnh, đã backup an toàn
- [ ] `POSTGRES_PASSWORD` mạnh, không dùng mặc định
- [ ] `PUBLIC_PORTAL_URL` / `Jwt:Issuer` là HTTPS thật
- [ ] Google redirect URI đúng domain production
- [ ] Đã rotate secret demo hoặc tắt `SEED_DEMO_DATA`
- [ ] TLS hoạt động, HSTS bật (Caddyfile đã có)
- [ ] Backup DB định kỳ
- [ ] Log không chứa bí mật
- [ ] Đã thử đăng nhập, mở app, đổi token end-to-end trên domain thật

## 10. Gỡ lỗi

| Triệu chứng | Xử lý |
| --- | --- |
| Caddy không lấy được cert | DNS chưa trỏ đúng, port 80/443 bị chặn |
| API lỗi kết nối DB | kiểm tra `postgres` healthy, mật khẩu trong `.env` |
| `502` từ Caddy | container `api`/`web` chưa lên; `docker compose ps` |
| Login Google xong quay về login | sai `Auth__FrontendUrl`, cookie Secure/SameSite, `Jwt__Issuer` |
| App báo invalid signature | JWKS cache cũ hoặc key bị sinh lại (volume `keys` mất) |
| Mọi user bị đăng xuất sau deploy | DataProtection keys không persist (`DataProtection__KeysPath`) |
