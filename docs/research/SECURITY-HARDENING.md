# SammiCrawl — nâng cấp bảo mật ngày 25/10/2026

## Phạm vi

Hermes đã đọc snapshot 44 file đã loại bí mật, với context thực tế **159.744 token**. Alias nghiên cứu riêng là `sammicrawl-security-hermes:156k`; `/api/ps` xác nhận chỉ một model và `size == size_vram` (19.911.388.690 byte). Model gốc và profile Hermes chính không bị thay đổi.

Session: `20261025_080639_ac7042`. Lượt thu thập nguồn kết thúc exit 1 nhưng có bản phân tích; lượt phản biện/chốt cùng session kết thúc exit 0. Các kết luận model được đối chiếu lại với mã nguồn, tài liệu và kiểm tra thực tế; không coi câu trả lời của model là bằng chứng đã kiểm thử.

Đã áp dụng năm nhóm sửa ở local, chưa commit/push. Đây là một đợt hardening có phạm vi, không phải chứng nhận đã pentest toàn diện hay không còn lỗ hổng.

## Đã thay đổi

| Vấn đề | Bằng chứng trước sửa | Thay đổi | Kiểm tra |
|---|---|---|---|
| Media chưa cần đăng nhập | HEAD một JPEG đã crawl, không có cookie, trả 200 trên localhost:5080 | Xác thực trước media; public provider loại đường dẫn media đã chuẩn hóa; giữ range cho video; file không tồn tại trả 404 | GET/HEAD ẩn danh trả 401; phiên hợp lệ trả 200/range 206; đường dẫn thay thế không lộ nội dung; no-store |
| API dùng cookie chưa có token CSRF | SameSite Strict, JSON và không mở CORS đã giảm rủi ro thông thường, nhưng chưa kiểm tra token | GET `/api/auth/csrf`; header `X-CSRF-TOKEN` cho các method thay đổi dữ liệu dưới `/api`; frontend tự lấy/reset token khi đổi danh tính và không tự gửi lại mutation | Thiếu/sai token, token khác cookie hoặc token ẩn danh sau login đều bị chặn trước handler; POST/DELETE đúng token vẫn hoạt động |
| Login/setup chưa giới hạn rõ | Chỉ có limiter cho remote AI; chưa có trần độ dài email/password | 10 request/phút theo địa chỉ peer thực, không tin `X-Forwarded-For` do client khai; body 16 KiB; email tối đa 254 và password tối đa 1024 ký tự; ticket mới 8 giờ, không sliding | AuthController thật trong fixture chặn body lớn trước auth service; vượt quota trả 429 và Retry-After; kiểm tra local trả 400/413 cho đầu vào quá dài |
| Cache và nhúng trang | Chưa có quy tắc chung cho phản hồi nhạy cảm | no-store cho API/bridge/media; nosniff; X-Frame-Options DENY; CSP frame-ancestors/object-src/base-uri; Referrer-Policy no-referrer | Header được kiểm tra trong fixture/local. CSP này là chính sách hẹp, không phải biện pháp chặn mọi XSS |
| Media tải hết vào RAM rồi mới kiểm tra kích thước | `APIRequest.BodyAsync()` chạy trước kiểm tra 50 MB | ResponseHeadersRead; đọc từng khối 64 KiB; tối đa 50 MB + 1 byte; timeout 30 giây toàn lượt; không redirect; chỉ HTTPS:443 trên domain cho phép; cookie đúng phạm vi URL và giữ UA trình duyệt; file dở chỉ đổi tên khi hoàn tất | Luồng giả vượt giới hạn bị dừng, không để lại file dở; media hợp lệ được lưu hoàn chỉnh; không gọi Facebook |

Bộ xử lý lỗi chung cũng được sửa để giữ mã của `BadHttpRequestException`, đặc biệt 413, thay vì đổi thành 500. Request bị client hủy không được ghi như lỗi server 500.

## Kiểm tra và cách chạy

```powershell
docker build -f tests/SecurityChecks/Dockerfile -t sammicrawl-security-checks .
docker run --rm sammicrawl-security-checks
```

**43 kiểm tra đạt.** Bộ kiểm tra liên kết trực tiếp pipeline, đăng ký security, AuthController và helper tải media từ source. Auth service, HTTP response/stream và filesystem là fixture riêng. Không dùng PostgreSQL, Facebook, Ollama, khóa hay cookie thật; không expose port container ra host. Xem `tests/SecurityChecks/README.md`.

Frontend TypeScript/Vite và Docker build đạt. Đã triển khai app local, giữ volumes/key/media. Kiểm tra UI bằng demo công khai: đăng xuất/đăng nhập thành công, ảnh nguồn vẫn tải khi có phiên. Không sửa/duyệt/rewrite/cào/đăng bài để thử. Các request HTTP quá dài bị chặn trước truy vấn đăng nhập; không brute-force tài khoản thật.

`npm audit --omit=dev` không báo advisory cho dependency production. NuGet kiểm tra cả dependency bắc cầu, nguồn api.nuget.org, không báo package dễ bị tấn công tại lượt kiểm tra. Điều này không bảo đảm không có lỗ hổng hoặc bao phủ mọi dependency phát triển, image hệ điều hành và runtime bên ngoài.

## Nguồn và các kết luận bị bác bỏ

- [Microsoft: static files](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0): vị trí static middleware ảnh hưởng kiểm tra quyền. HEAD 200 trên loopback không chứng minh rò rỉ ra Internet.
- [Microsoft: antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0): token/header phải được kiểm tra thực sự. Chưa chứng minh một khai thác CSRF thành công trên bản cũ; token là lớp bảo vệ bổ sung.
- [OWASP: Authentication](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html): giới hạn thử đăng nhập và bảo vệ phiên.
- [OWASP: LLM Prompt Injection](https://cheatsheetseries.owasp.org/cheatsheets/LLM_Prompt_Injection_Prevention_Cheat_Sheet.html): bài cào/ảnh là dữ liệu không đáng tin. Quyền và duyệt phải do backend kiểm soát; blacklist từ khóa hoặc một canary không chứng minh đã chống được injection.
- [Ollama: create model](https://docs.ollama.com/api/create): alias riêng đặt num_ctx 159744 vì model gốc đóng mặc định 98304.

Không chấp nhận các kết luận sai của Hermes: vị trí static trong Program.cs; tự động bảo vệ mọi JSON API; domain allowlist bảo đảm SSRF tuyệt đối; crawler chắc chắn không bị Facebook giới hạn; shared project mặc nhiên là lỗi multi-tenant IDOR. Raw log nằm trong `work/security-research`, không đưa vào repo. Query tìm web chỉ dùng chủ đề bảo mật chung, không gửi snapshot hay bí mật ra web.

## Giới hạn và việc tiếp theo

1. **Secret/demo:** connection string được track và demo công khai vẫn còn theo quyết định trước của chủ repo. Trước khi mở UI ra Internet cần owner quyết định rotation/tách secret/xử lý demo. Không tự reset credential hoặc sửa lịch sử Git trong đợt này; báo cáo không chứa giá trị secret.
2. **Thu hồi phiên:** giới hạn 8 giờ áp dụng ticket mới; không thu hồi ticket cũ. Chưa có security stamp/session revocation trung tâm. Không suy ra đổi mật khẩu sẽ tự hủy mọi phiên.
3. **Quyền shared project:** người đã đăng nhập vẫn cùng quản lý tài khoản. Chức năng xem cookie được giữ theo yêu cầu; no-store không thay thế phân quyền hoặc xác thực lại nếu sau này thêm vai trò/tenant.
4. **Triển khai public:** cần cấu hình TLS và trusted proxy đúng môi trường, cùng phương án rate limit nhiều replica và backup/recovery. Không bật trust-any-proxy, không public Ollama. SecurePolicy SameAsRequest giữ HTTP loopback hoạt động; phía TLS proxy phải được cấu hình trước khi mở UI public.
5. **Media:** streaming đã chặn tải body không giới hạn vào RAM, nhưng chưa có DNS/IP pinning, giới hạn pixel/codec hay sandbox phân tích. Không thử lại Facebook thật trong đợt này. URL hết hạn, blob video và file trên 50 MB vẫn được báo thiếu media. Bản sao/ảnh đã cache trước sửa không thể bị thu hồi bằng no-store.
6. **LLM:** prompt injection vẫn cần người đối chiếu ảnh/nội dung; điểm Clef không phải quyền đăng. Giữ duyệt phía backend. Chưa có publisher worker và không đo attack-success-rate của model.

## Trạng thái vận hành

Ba campaign worker Zalo đã được pause qua cơ chế chính thức theo lệnh người dùng; khi kiểm tra cuối, cả ba có `owner_pause`, không có recipient đang processing và không còn process worker. Không thay sent/unknown, không tự resume Zalo.

Hermes đã hoàn tất và được unload. Lịch kiểm tra 10 phút được tắt sau khi chốt kiểm tra. Không commit/push; Zalo chờ người dùng yêu cầu chạy lại.
