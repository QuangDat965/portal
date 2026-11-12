# ADR-0002: Bổ sung đăng nhập bằng email/mật khẩu cục bộ

- **Trạng thái:** Accepted
- **Ngày:** 2026-11-12
- **Người quyết định:** Tech lead
- **Liên quan:** `docs/auth-flow.md`, `docs/security.md`, `AGENTS.md`

## 1. Bối cảnh

ADR-0001 chọn Google OAuth làm phương thức đăng nhập duy nhất cho portal, kèm dev login
chỉ dùng khi phát triển. Trong thực tế cần thêm một phương thức đăng nhập nội bộ:

- Một số tài khoản không có Google (tài khoản dịch vụ, tester, môi trường mạng nội bộ
  không ra được Google).
- Cần đăng nhập được khi Google OAuth chưa cấu hình hoặc tạm thời không khả dụng.
- Vẫn giữ nguyên mô hình phiên và cầu nối launch code → JWT cho app vệ tinh.

## 2. Phương án đã cân nhắc

### A. Chỉ giữ Google + dev login

- **Ưu:** ít bề mặt tấn công nhất, không phải quản lý mật khẩu.
- **Nhược:** phụ thuộc hoàn toàn Google; dev login không được phép bật ở production.
- **Kết luận:** không đáp ứng nhu cầu.

### B. Thêm nhà cung cấp OIDC nội bộ (Keycloak/Authentik)

- **Ưu:** chuẩn hoá, có MFA, quản lý vòng đời mật khẩu.
- **Nhược:** thêm hệ thống lớn phải vận hành; quá tải với một VPS nhỏ (đã loại ở ADR-0001).
- **Kết luận:** để dành cho giai đoạn sau.

### C. Dùng ASP.NET Core Identity có sẵn để xác thực email/mật khẩu (đã chọn)

- Bật `PasswordSignInAsync` trên `AppUser` hiện có; không thêm runtime hay DB mới.
- Admin đặt/đổi mật khẩu qua API quản trị; user Google chưa có mật khẩu sẽ không đăng
  nhập được bằng phương thức này cho tới khi được đặt.
- Giữ nguyên cookie `portal.session` và toàn bộ luồng launch code/JWT.

## 3. Quyết định

| Thành phần | Quyết định |
| --- | --- |
| Endpoint đăng nhập | `POST /api/auth/password-login` (email hoặc username + mật khẩu) |
| Xác thực | `SignInManager.PasswordSignInAsync`, lockout khi sai nhiều lần |
| Policy mật khẩu | tối thiểu 12 ký tự, không bắt buộc ký tự đặc biệt |
| Lockout | 5 lần sai → khoá 5 phút |
| Rate limit | `login` 10 request/phút/IP |
| CSRF | bắt buộc (`X-CSRF-TOKEN` + cookie `portal.csrf`) |
| Quản lý mật khẩu | chỉ admin: `PUT /api/admin/users/{id}/password`, có audit |
| Seed | `Seed:AdminPassword` (dev có giá trị mặc định; production để trống/secret) |
| Phiên & token | không đổi so với ADR-0001 |

## 4. Hệ quả

### Tích cực

- Đăng nhập được khi không có Google; phù hợp tài khoản nội bộ/dịch vụ.
- Không thêm thành phần hạ tầng; tận dụng Identity đã có sẵn (bảng `AspNetUsers`).
- Vẫn giữ nguyên hợp đồng JWT/JWKS với các app vệ tinh.

### Tiêu cực / đánh đổi

- Tăng bề mặt tấn công: phải bảo vệ mật khẩu trước brute force, credential stuffing.
- Chưa có MFA cho tài khoản dùng mật khẩu.
- Admin trở thành điểm quản trị nhạy cảm: đặt mật khẩu cho user khác.
- Cần quy trình đổi mật khẩu/khôi phục khi quên (hiện admin reset, chưa có self-service).

## 5. Biện pháp giảm thiểu

- Hash bằng Identity (PBKDF2 + salt riêng từng user).
- Rate limit theo IP + lockout theo tài khoản.
- Thông báo lỗi chung, không lộ email/username có tồn tại.
- CSRF cho endpoint đăng nhập.
- Audit thao tác đặt lại mật khẩu; không log mật khẩu.
- Không commit mật khẩu thật; `Seed:AdminPassword` production lấy từ secret manager.

## 6. Tiêu chí xem lại

- Cần MFA/SSO thống nhất cho cả tài khoản mật khẩu → chuyển sang OIDC nội bộ.
- Cần self-service đổi/quên mật khẩu → bổ sung luồng email reset có token.
- Phát hiện credential stuffing quy mô lớn → thêm CAPTCHA/WAF hoặc bỏ mật khẩu.
