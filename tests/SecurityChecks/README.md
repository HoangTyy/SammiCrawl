# Security checks

Chạy từ thư mục gốc repo:

```powershell
docker build -f tests/SecurityChecks/Dockerfile -t sammicrawl-security-checks .
docker run --rm sammicrawl-security-checks
```

Hoặc khi có .NET 10 SDK: `dotnet run --project tests/SecurityChecks/SecurityChecks.csproj` (cần port 8081 local trống). Ưu tiên Docker để cách ly cổng và filesystem.

Bộ kiểm tra liên kết code production của pipeline, đăng ký security, AuthController và helper download. Auth service là fixture không có DB. Cookie/key giả chỉ tồn tại trong container; media nằm trong thư mục tạm; HTTP handler giả không ra mạng. Không expose cổng container và không đọc .env/appsettings/key volume thật. Chương trình thoát khác 0 nếu có assert thất bại.

43 assert bao phủ static public/private GET/HEAD/range/cache, đường dẫn media chuẩn hóa, token CSRF gắn với danh tính/cookie, DELETE không body, gateway port/key, phạm vi WorkerKey, AuthController body limit, auth rate limit/Retry-After, giới hạn stream và cleanup/lưu file hoàn tất. Các lần login sai là auth service giả, không phải tài khoản thật.

Không bao phủ truy vấn EF/quyền nhiều tenant, Facebook/CDN/DNS thật, chất lượng AI/prompt injection benchmark, TLS/Cloudflare, secret rotation, tấn công phân tán hoặc giới hạn pixel/giải nén. CSP ở lượt này chỉ có frame-ancestors/object-src/base-uri; không coi là full CSP chống XSS.

## SignalR và UI nhiều phiên (28/10/2026)

Bộ kiểm tra hiện có 48 assertion: thêm anonymous hub negotiate 401, thiếu CSRF 403, cookie+CSRF hợp lệ, gateway port không lộ hub, hai client WebSocket cùng nhận `DataChanged` không kèm bản ghi/bí mật. Dùng hub và broadcaster production, không dùng database thật.

Chế độ `--ui-fixture` phục vụ frontend build bằng dữ liệu mẫu trong RAM (một preset), chỉ nằm trong image kiểm thử. Từ thư mục gốc sau `npm --prefix frontend run build`:

```powershell
docker run --rm --name sammicrawl-live-ui-check -p 127.0.0.1:15080:8080 --mount "type=bind,source=$((Resolve-Path frontend/dist).Path),target=/ui-dist,readonly" sammicrawl-security-checks -- --ui-fixture
```

Mở hai browser context riêng tại http://localhost:15080. Trong mỗi context, lấy `/api/auth/csrf` rồi POST `/api/fixture-login` với `X-CSRF-TOKEN`, reload để vào shell. GET/PUT `/api/records/presets/11111111-1111-1111-1111-111111111111` dùng preset giả. Đổi tên ở phiên A: danh sách phiên B phải tự đổi; modal đang sửa ở B giữ nội dung và focus, hiện cảnh báo. Không dùng route fixture này ở app production.

Khi thử mất kết nối bằng pause/unpause container, không cần khởi động lại fixture: restart tạo content root tạm mới nên cookie cũ không còn hợp lệ. Đây là đặc điểm fixture, không phải kiểm tra khôi phục phiên production.

Kiểm tra UI đã thực hiện với hai browser context độc lập: thay đổi preset giả từ A xuất hiện ở cả hai danh sách; B giữ nội dung chưa lưu và focus trong modal, hiện cảnh báo. Pause/unpause container fixture khiến client chuyển reconnecting rồi tự kết nối lại. Viewport 390 px không tràn ngang, modal rộng 366 px, nút và focus vẫn dùng được. App localhost cũng xác nhận trạng thái Đồng bộ trực tiếp. Không thử cào/rewrite/đăng Facebook hoặc sửa dữ liệu DB thật để kiểm thử.

Kiểm tra giới hạn cào: thêm 6 assertion cho 1 tài khoản/10 bài với tổng100 bị chặn, đúng10 được phép, nhiều tài khoản, không tài khoản, trần1000 và giảm giới hạn mỗi tài khoản. Tổng hiện tại:54 assertion; quy tắc CrawlCapacity được link trực tiếp từ production, không chạy Facebook hay tạo task thật.

Cào + Clef: tổng70 assertion, thêm16 kiểm tra availability (offline/thiếu model/đúng model), lỗi/busy/kết quả200 không hợp lệ không tính đánh giá, giữ điểm/loại soft-delete/giữ review, tiến độ90/99/100 và sửa task cũ hoàn thành sai. Dùng CrawlEvaluationState production trong fixture; chưa chứng minh Facebook/AI inference thật thành công.

Tổng73 assertion: thêm3 kiểm tra readiness Bonsai offline/thiếu27b (chỉ có9b)/đúng27b; không tải model hoặc chạy suy luận thật.

GatewayRetryChecks dùng HttpMessageHandler giả với client production:83 assertion tổng; bao phủ502 rồi thành công,502 lặp tối đa2lần, không retry409/429/auth/output/rewrite và phân loại timeout/JSON. Không dùng DB, Ollama hoặc Facebook thật.

Nhóm nguồn: tổng92 assertion; thêm9 kiểm tra URL nhóm, chống URL giả/post/navigation, từ khóa bounded, ID ổn định theo brand, accepted không gồm review/deleted, kết luận kênh và chặn hoàn tất khi đánh giá kênh chưa xong. Không chạy Facebook/AI thật.
