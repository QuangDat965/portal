# Mèo Nổ Online

App vệ tinh .NET 10 + React, đăng nhập bằng Portal SSO như Taskboard/Team Notes.
Không dùng chung cookie với Portal. Người chơi cần role được cấp quyền cho app;
seed mặc định cấp cho member khi `Seed:DemoData=true`.

## Phát triển

1. Tạo database PostgreSQL riêng, ví dụ `createdb -U portal -h localhost exploding_kittens`.
2. Chạy Portal backend như hướng dẫn phát triển. Seed app `exploding-kittens` có BaseUrl
   `http://localhost:5185`, client secret mặc định chỉ dùng local. Để chơi qua Vite,
   đặt `Seed__KittensAppUrl=http://localhost:5186` khi chạy Portal.
3. `dotnet run --project demo-apps/exploding-kittens/api` (cổng 5185, tự apply migration).
4. `cd demo-apps/exploding-kittens/web && pnpm install --frozen-lockfile && pnpm dev` (5186).
5. Mở app từ Launcher. Dùng tài khoản/trình duyệt riêng cho mỗi người chơi.

Build frontend: `pnpm typecheck && pnpm build`. Để serve SPA trực tiếp từ API local,
copy nội dung `web/dist/` vào `api/wwwroot/`; Portal BaseUrl phải trỏ cổng API 5185.
Dockerfile API tự build/copy SPA, không cần build thủ công trước.

Cấu hình API: `ConnectionStrings:Postgres`, `Portal:Url`, `Portal:Issuer`,
`Portal:FrontendUrl`, `Portal:ClientId`, `Portal:ClientSecret`, `Cookie:Secure`,
`Cookie:SessionHours` (mặc định 8). Issuer phải khớp Portal. Production bật Secure cookie.
App dùng schema `exploding_kittens` và bảng migration riêng nên có thể dùng cùng database
với Portal; không cần thêm PostgreSQL server. User DB cần quyền tạo schema/bảng.

## Luật MVP

- 2–5 người, chỉ chủ phòng được bắt đầu. Mã mời 6 ký tự; người đã vào có thể dùng lại
  mã để mở lại phòng, kể cả khi ván đã bắt đầu. Phòng đủ 5 người không nhận thêm.
- Mỗi người có 4 lá ngẫu nhiên an toàn + 1 Giải cứu. Tổng 6 Giải cứu; phần dư ở bộ rút.
  Sau khi chia thêm đúng `số người - 1` Mèo nổ rồi xáo bài.
- Đánh bài không kết thúc lượt trừ Skip/Attack. Rút một lá trả một lượt rút đang nợ.
- Giải cứu dùng tự động khi rút Mèo nổ, đưa mèo trở lại vị trí ngẫu nhiên (kể cả đáy).
  Không có Giải cứu: người chơi bị loại, bỏ hết bài; nợ rút được xoá.
- Skip trả một lượt rút đang nợ. Attack chuyển nợ còn lại + 2 nếu đang nợ nhiều lượt,
  hoặc 2 nếu bình thường, sang người tiếp theo còn sống.
- Shuffle xáo bộ rút; Xem tương lai trả riêng tối đa 3 lá theo thứ tự rút, không đổi bộ rút.
- Favor lấy ngẫu nhiên một lá của một đối thủ còn sống, còn bài. Không được tự nhắm mình.
- Chưa có Nope, combo, chọn vị trí đặt mèo, rời ghế, chơi lại trong cùng phòng hay timeout.
  Đóng trình duyệt không bỏ ghế, vẫn giữ lượt; về sảnh không xoá phòng/ván.
- Người cuối cùng còn sống thắng. Trạng thái phòng và ván được lưu cùng transaction.

## API

Tất cả route dưới đây cần session cookie riêng của app. POST cần header `X-CSRF-TOKEN`,
lấy từ `GET /api/auth/csrf`. User id luôn lấy từ claim `sub` của session.

| Route | Body | Response |
| --- | --- | --- |
| `POST /api/rooms` | `{ name }` (tối đa 80 ký tự) | GameView |
| `POST /api/rooms/join` | `{ inviteCode }` | GameView |
| `GET /api/rooms/{id}/state` | — | GameView của người gọi |
| `POST /api/rooms/{id}/start` | — | GameView |
| `POST /api/games/{id}/draw` | — | `{ view, exploded, defused }` |
| `POST /api/games/{id}/play` | `{ cardType, targetUserId? }` | `{ view, futureCards }` |

GameView chỉ chứa tay bài người gọi, số lá đối thủ, trạng thái lượt, số lá bộ rút và
người thắng; không chứa bộ rút hoặc các tay bài khác. Lỗi nghiệp vụ trả 400 `{ error }`
với mã lỗi để frontend dịch vi/en; chưa đăng nhập trả 401. Mutation được tuần tự hoá
bằng advisory transaction lock trong PostgreSQL, hoạt động qua nhiều API instance.

UI poll state mỗi 2 giây; phòng đang mở được ghi trong sessionStorage theo user.
SignalR `/api/hub/game` hỗ trợ `JoinRoom`, `LeaveRoom`, `RequestState`. Join/RequestState
kiểm tra membership và chỉ gửi `State` cho connection gọi. `RoomChanged` chỉ báo id phòng;
không phát tay bài/future qua group chung. Khi triển khai nhiều replica, UI polling vẫn
cập nhật; notification SignalR cần backplane nếu muốn kết nối xuyên replica.

## Kiểm tra và deploy

```bash
dotnet build backend/Portal.slnx
dotnet test backend/Portal.slnx
dotnet test demo-apps/exploding-kittens/tests
# Chỉ trỏ tới database kiểm thử riêng; test sẽ tạo schema và lưu phòng test.
EK_TEST_POSTGRES='Host=localhost;Database=kittens_test;Username=portal;Password=portal' \
  dotnet test demo-apps/exploding-kittens/tests
cd demo-apps/exploding-kittens/web
pnpm typecheck
pnpm build
```

Compose bổ sung service `kittens`, Caddy domain `KITTENS_DOMAIN`,
`PUBLIC_KITTENS_URL`, `KITTENS_CLIENT_SECRET`. Đặt cùng secret cho seed và app;
secret production cấp/rotate qua Admin, không thay bằng cách seed lại client đã tồn tại.
Cookie keys lưu ở volume `kittens_keys` để restart không làm mất phiên.

## Giao diện bàn chơi 2.5D và thứ tự hiệu ứng

Frontend dùng `motion/react` (Motion 14) + CSS perspective, không có WebGL.
Artwork mèo và các lá bài là SVG gốc trong `CardArt.tsx`. Các component trình bày:
`Card`, `CardHand`, `CardStack`, `PlayerSeat`, `GameTable`, `GameEffects`.

- Desktop: bàn gỗ nghiêng nhẹ, đối thủ ở phía trên, bộ rút/chồng bỏ ở giữa,
  bài trên tay xòe cong phía dưới; hover nâng/tilt bài và click đánh lá có thể dùng.
- Touch: chạm để xem/chọn, bấm nút đánh để xác nhận; Favor mở chọn đối thủ.
  Tay bài nhiều lá chia cửa sổ 10 lá (desktop) hoặc 5 lá (mobile), có nút trước/sau
  và vuốt ngang. Lá chọn được nâng lên và giữ ở trên các lá khác.
- HUD gọn chứa mã phòng có thể copy, luật chơi, ngôn ngữ và về sảnh. Không có API
  rời ghế; về sảnh vẫn giữ ghế/lượt như trước.
- `MotionConfig reducedMotion="user"` và `prefers-reduced-motion` bỏ tilt, camera,
  bay bài, rung và hạt hiệu ứng khi người dùng yêu cầu giảm chuyển động. Hành động
  vẫn hoàn tất, feedback chữ và cập nhật trạng thái vẫn hiển thị.

API giữ nguyên route/request/response cũ và bổ sung các field GameView:

| Field | Nội dung |
| --- | --- |
| `revision` | Số thứ tự tăng trong cùng transaction với hành động thành công |
| `recentEvents` | Tối đa 32 sự kiện công khai, có `sequence`, `actorUserId`, `kind`, `cardType`, `targetUserId` |
| `discardTop`, `discardCount` | Lá trên cùng và số lá đã bỏ |

`kind`: `started`, `drawn`, `played`, `defused`, `exploded`.
`cardType` chỉ có khi đánh bài công khai; rút bài không tiết lộ loại bài.
Favor không tiết lộ loại lá lấy được; Xem tương lai vẫn chỉ trả qua response riêng.
Dữ liệu sự kiện lưu bằng migration `PublicGameEvents`; các ván cũ bắt đầu với
revision 0 và history trống, không thay bài/luật/lượt đang có.

Frontend giữ snapshot server và state trình bày riêng trong `presentationReducer`.
Sự kiện được xếp theo sequence, loại trùng giữa poll và mutation response, bỏ
snapshot cũ; cập nhật tay bài sau khi hiệu ứng kết thúc. Khóa thao tác khi mutation
hoặc animation còn chạy. Request polling hỗ trợ AbortSignal. Mất history hoặc tồn
hơn 8 sự kiện chờ thì đồng bộ thẳng snapshot mới, không phát lại backlog dài.
Reload/vào lại phòng không diễn lại các sự kiện cũ. Định danh lá trên tay là state
trình bày, giữ ổn định giữa các snapshot và phân biệt các bản sao cùng loại.

Motion AI Kit được cài ở `web/.agents/skills/motion/`, cấu hình MCP cho Codex ở
`web/.codex/config.toml`. Dùng các best practices trong skill khi sửa animation.
Các effect Nope không được kích hoạt vì luật MVP chưa hỗ trợ lá này.

Kiểm tra bổ sung: `cd demo-apps/exploding-kittens/web && pnpm test` cho event ordering,
deduplication, resync, hand identity và reload. Test PostgreSQL xác nhận revision
được lưu atomically và public events không chứa bài rút/bài tương lai.
