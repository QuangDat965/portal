# Data model

Database: **PostgreSQL 17** qua EF Core 10. Toàn bộ bảng do `PortalDbContext` quản lý,
migration nằm ở `backend/src/Portal.Infrastructure/Persistence/Migrations`.

```
IdentityUser ──< IdentityUserRole >── IdentityRole
      │                                    ▲
      │                                    │ app_access.role_id
      │                              ┌─────┴──────┐
      │                              │  apps      │
      └──< launch_codes >───────────│            │
                                     │            │────< app_clients
                                     └────────────┘
              audit_logs (user_id, app_id chỉ để tra cứu, không FK)
```

## 1. Bảng Identity (ASP.NET Core Identity)

| Bảng | Ghi chú |
| --- | --- |
| `AspNetUsers` | `Id` (Guid), `Email`, `DisplayName`, `AvatarUrl`, `CreatedAt`, `LastLoginAt` |
| `AspNetRoles` | Seed sẵn: `admin`, `member` |
| `AspNetUserRoles` | N-N user ↔ role |
| `AspNetUserLogins` | Liên kết Google (`LoginProvider = "Google"`, `ProviderKey = sub`) |

Portal **không dùng mật khẩu**: chỉ external login (Google) hoặc dev login.
Vì vậy `PasswordHash` luôn null với user thật.

### AppUser

```csharp
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
```

`DisplayName` là tên hiển thị (từ Google `name`), `UserName` = email.

## 2. apps

| Cột | Kiểu | Ràng buộc |
| --- | --- | --- |
| `Id` | uuid | PK |
| `Slug` | varchar(80) | unique, `^[a-z0-9](?:[a-z0-9-]{0,78}[a-z0-9])?$` |
| `Name` | varchar(200) | not null |
| `Description` | varchar(1000) | nullable |
| `IconUrl` | varchar(1000) | nullable |
| `BaseUrl` | varchar(500) | not null, http/https tuyệt đối |
| `CallbackPath` | varchar(200) | not null, mặc định `/auth/callback`, bắt đầu bằng `/` |
| `Status` | varchar(20) | `Active` / `Disabled` (lưu dạng string) |
| `SortOrder` | int | thứ tự hiển thị |
| `CreatedAt` / `UpdatedAt` | timestamptz | |

`Slug` là định danh công khai ổn định, cũng chính là `client_id` mặc định.

**Quy ước URL:** `BaseUrl` không có dấu `/` cuối; khi launch, portal ghép
`BaseUrl + CallbackPath + ?code=...`.

## 3. app_access

Bảng nối **role → app**: user có role nào thì thấy app gán cho role đó.

| Cột | Kiểu | Ràng buộc |
| --- | --- | --- |
| `AppId` | uuid | PK phức hợp, FK → `apps` (cascade) |
| `RoleId` | uuid | PK phức hợp, index |
| `CreatedAt` | timestamptz | |

Admin **bỏ qua** bảng này: luôn thấy mọi app `Active`.

## 4. app_clients

Thông tin client để backend của app đổi launch code lấy JWT.

| Cột | Kiểu | Ràng buộc |
| --- | --- | --- |
| `Id` | uuid | PK |
| `AppId` | uuid | FK → `apps` (cascade) |
| `ClientId` | varchar(100) | unique |
| `ClientSecretHash` | varchar(200) | not null |
| `CreatedAt` | timestamptz | |
| `RotatedAt` | timestamptz | nullable |

- `ClientSecretHash` = `HMACSHA256(secret, Security:SecretPepper)`, hex.
- So sánh bằng `CryptographicOperations.FixedTimeEquals`.
- Secret plaintext **chỉ trả một lần** khi tạo app hoặc rotate; DB không giữ.
- Một app hiện dùng một client (lấy client đầu tiên theo `CreatedAt`).

> Pepper là bí mật toàn cục. Đổi pepper sẽ làm **tất cả** secret cũ không verify được
> → phải rotate toàn bộ client.

## 5. launch_codes

| Cột | Kiểu | Ràng buộc |
| --- | --- | --- |
| `Id` | uuid | PK |
| `CodeHash` | varchar(200) | unique, index |
| `UserId` | uuid | user được phát code |
| `AppId` | uuid | FK → `apps` (cascade) |
| `ExpiresAt` | timestamptz | index, `CreatedAt + 60s` |
| `UsedAt` | timestamptz | nullable |
| `CreatedAt` | timestamptz | |

- Code = 32 byte ngẫu nhiên (`RandomNumberGenerator`), base64url.
- Chỉ lưu hash HMAC-SHA256 (cùng pepper với client secret).
- Đổi code: `UPDATE launch_codes SET used_at = now WHERE id = ... AND used_at IS NULL`
  → đảm bảo một code chỉ đổi được một lần kể cả khi request song song.
- Code gắn với đúng `AppId`; client của app khác không đổi được.

## 6. audit_logs

| Cột | Kiểu | Ghi chú |
| --- | --- | --- |
| `Id` | bigint | identity |
| `UserId` | uuid | nullable |
| `AppId` | uuid | nullable |
| `Action` | varchar(100) | vd. `app.launch`, `app.token_issued` |
| `IpAddress` | varchar(100) | |
| `UserAgent` | varchar(500) | |
| `Metadata` | text | vd. slug, email + role |
| `CreatedAt` | timestamptz | index |

Action hiện có:

| Action | Khi nào |
| --- | --- |
| `app.launch` | user mở app, launch code được tạo |
| `app.token_issued` | đổi code thành công, JWT được phát |
| `admin.app_created` | tạo app |
| `admin.app_updated` | sửa app |
| `admin.app_deleted` | xoá app |
| `admin.client_rotated` | rotate client secret |
| `admin.user_roles_updated` | đổi role user |

## 7. Migration

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
export DOTNET_ROOT=/usr/local/share/dotnet   # nếu SDK cài ngoài /usr/share/dotnet

cd backend
dotnet ef migrations add <TenMigration> \
  -p src/Portal.Infrastructure \
  -s src/Portal.Api \
  -o Persistence/Migrations

dotnet ef database update -p src/Portal.Infrastructure -s src/Portal.Api
```

API tự chạy `Database.MigrateAsync()` khi khởi động (`DbInitializer`), nên ở môi trường
đơn giản không cần chạy `database update` thủ công. Với production nhiều instance, nên
chạy migration như bước deploy riêng để tránh race.
