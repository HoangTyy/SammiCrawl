# Bản local đầu tiên

## Chạy

Tại `D:\Project\SammiCrawl`:

```powershell
docker compose up -d --build
```

Mở http://localhost:5080 để vào dashboard ngay, không cần tạo tài khoản hoặc đăng nhập. Docker Compose bật LocalDevelopment__SkipLogin=true cho hostname localhost/127.0.0.1, không áp dụng với request có header proxy. Tắt cấu hình này khi triển khai host. Bridge LLM vẫn yêu cầu Bearer token.

- App: React theo giao diện gốc + .NET 10, cổng 5080 chỉ bind localhost.
- PostgreSQL từ xa: cấu hình `POSTGRES_CONNECTION` trong `.env`. TLS VerifyCA dùng chứng chỉ ghim tại `config/postgres/server.crt`; schema `sammicrawl`.
- SQL Server cũ giữ volume `sammicrawl_sammicrawl_sql` để khôi phục, chỉ khởi động khi dùng profile `legacy-sql`.
- Bí mật database và bridge: `.env` (gitignored); khóa mã hóa cookie ở volume riêng. Sao lưu cả SQL volume, key volume và `.env` để khôi phục phiên.
- Dừng riêng dự án: `docker compose stop`. Không dùng `down -v` nếu muốn giữ dữ liệu.

## Đã nối thật

Đăng ký quản trị lần đầu/đăng nhập/đăng xuất; cookie đăng nhập HttpOnly; lưu brand, tài khoản Facebook với vai trò crawl/publish; mã hóa session phía backend và không trả session về browser; lưu cấu hình; lưu tác vụ cào dưới trạng thái tạm dừng; log trong SQL; lịch sử rewrite; Bonsai 2 27B qua Ollama tại host.docker.internal:11434. Cấu hình Studio không chứa cookie được lưu lại trong localStorage và có thể lưu preset vào SQL. AI Rewriter chọn brand hoặc nhập chỉ dẫn riêng.

Lưu ý bản đầu dùng một tài khoản quản trị chung, chưa có phân quyền nhiều người. Dữ liệu cấu hình lưu trong bảng Documents JSON; cần chuyển thành bảng chuyên biệt và migration khi hoàn thiện nghiệp vụ. Chưa có sửa/xóa brand và account.

## Chưa triển khai

Worker crawl Facebook, kiểm tra session, tải ảnh/video, chống trùng, Clef chấm điểm/vision, phân công nhiều tài khoản, random khoảng nghỉ, repost/rewrite-to-publish, lịch đăng, duyệt bài, xuất Excel. Không có bài nào được cào/đăng từ bản này. Task lưu lại là paused, không tự chuyển thành running. Các khung pipeline/worker mô tả kế hoạch, không phải tiến trình đang hoạt động.

## Quy tắc AI

Bản local chạy 1 container app. Singleton semaphore từ chối request AI thứ hai với HTTP 409. Trước rewrite kiểm tra /api/ps, dỡ model đang nạp, xác minh model đã dỡ rồi mới chạy bonsai-2:27b. keep_alive=0 và lệnh unload sau tác vụ. Không mở thêm replica app hoặc chạy AI bên ngoài SammiCrawl nếu muốn duy trì điều kiện độc quyền model. Semaphore này không khóa được chương trình khác trực tiếp gọi Ollama.

## Bridge cho backend host

Đã chuẩn bị API có Bearer token:

- GET `/bridge/health`
- POST `/bridge/rewrite` với `{ "text": "...", "tone": "...", "mode": "rewrite", "instructions": "..." }`
- Header: `Authorization: Bearer <REMOTE_AI_KEY trong .env>`

Không trỏ public hostname trực tiếp tới cổng Ollama 11434. Với cloudflared đang chạy native trên Windows, thêm public hostname `ollama.khanhdc.id.vn` ở tunnel hiện có, origin `http://localhost:5080`; backend host gọi `https://ollama.khanhdc.id.vn/bridge/rewrite`. Nếu cloudflared chạy trong Docker, origin cần là host.docker.internal:5080 hoặc cùng Docker network phù hợp.

Máy hiện có token connector, nhưng chưa có quyền quản trị DNS/tunnel/API để tự thêm hostname. Hostname public CHƯA được cấu hình hay xác minh. Không thay đổi tunnel/DNS của dịch vụ hiện tại. Chỉ mở sau khi đã tạo tài khoản quản trị; khi host public cần thêm rate limiting, khóa setup, cấu hình trusted proxy/HTTPS và bảo vệ Cloudflare Access trước khi dùng production. Endpoint bridge hiện chỉ hỗ trợ rewrite; Clef/vision sẽ bổ sung sau khi xác minh giao thức model.

## Kiểm chứng

Frontend production build; .NET publish trong Docker; SQL healthy; API smoke test đăng nhập/CRUD create-list/mã hóa session/401; bridge token guard; Bonsai trả kết quả tiếng Việt và /api/ps trống sau khi hoàn tất; giao diện đã mở và kiểm tra trong trình duyệt. Kết quả test ở `work/` (gitignored), không chứa token hoặc mật khẩu production.

## Cập nhật workflow và tài khoản Facebook

Menu Tài khoản Facebook riêng: thêm/sửa tên, UID, vai trò cào hoặc đăng, cập nhật cookie, bật/tạm ngưng tài khoản. Để trống cookie khi sửa giữ nguyên phiên; API không trả cookie đã lưu.
Studio chọn nhiều tài khoản cào và một tài khoản đăng riêng, chọn brand. Cấu hình lưu: số bài tối đa 1–1000, số lần cuộn 1–2000, delay min/max 500–120000 ms, min likes/comments, từ khóa, mô tả chủ đề; điểm Clef tối thiểu, số bài chọn, nhánh repost/rewrite, chỉ dẫn Bonsai, temperature và duyệt trước đăng. Ảnh/video gốc bắt buộc. Lưu task/preset vào SQL và tự nhớ cấu hình không chứa cookie trên browser.
Backend từ chối min > max, sai vai trò, tài khoản tạm ngưng. Config này là đặc tả công việc cho worker tương lai; chưa thực hiện random chờ/cào/lọc/đăng. Những cấu hình browser cũ có scrollDelay được đổi sang hai giá trị ms bằng nhau khi mở Studio.

## Quản lý không gian dự án

Thương hiệu có thẻ hồ sơ, số liệu tổng quan, tìm kiếm, lọc ngôn ngữ, tạo/sửa mô tả, giọng điệu, phong cách, đối tượng, CTA, từ khóa bắt buộc/cấm và quy tắc. Các trường này được đưa vào chỉ dẫn khi chọn brand ở AI Rewriter.
Cấu hình workflow đã lưu có chỉnh sửa thông số và áp dụng vào Studio. Lịch sử AI cho sửa nội dung, giữ nguyên nguồn và model. Cả ba loại bản ghi đều có xóa vào thùng rác và khôi phục, không xóa vĩnh viễn. ID/ngày tạo được giữ nguyên khi sửa; backend kiểm tra khoảng delay và giới hạn số.

## Studio theo từng bước

Studio tách sáu bước: Nguồn và tài khoản; Thu thập; Clef lọc; Bài được chọn; Repost/Rewrite; Duyệt và đăng. Workflow có tên, nguồn URL, lưu/mở/cập nhật cấu hình và tạo mới. Giới hạn mỗi tài khoản, nghỉ giữa lượt min/max, độ dài Bonsai min/max, Page đích được lưu riêng. Dashboard tạo nhanh hiện mở Studio với URL thay vì tạo tác vụ giả.
Nút Lưu workflow chỉ lưu định nghĩa trong presets. Kiểm tra trước chạy gọi POST /api/workflows/{id}/preflight: kiểm tra định dạng URL, hồ sơ brand, đúng vai trò và có phiên đã lưu, model local; báo chưa xác minh phiên/quyền đăng, kho media và worker chưa có.
POST /api/workflows/{id}/runs tạo bản ghi runs với bản chụp cấu hình, trạng thái blocked, danh sách điều kiện thiếu và các stage not_started. Lịch sử không thay đổi khi sửa định nghĩa. Chưa có thực thi crawler/Clef/publish; chưa có kết quả để chọn nhánh theo từng bài. Không có lần chạy nào được báo hoàn thành giả. Ràng buộc một model hiện áp dụng cho API rewrite có sẵn; điều phối Clef→Bonsai toàn workflow vẫn chưa triển khai.

## Giao diện đơn giản: Cào bài → Danh sách bài → Duyệt

Đã thay wizard sáu bước bằng form cào hai phần và nút Chạy cào bài. Chọn brand/nhóm đích, URL Page nguồn, thông số cào và nghỉ min–max. Backend tự lấy toàn bộ tài khoản role=crawl đang bật tại thời điểm bấm chạy, không nhận lựa chọn tài khoản từ browser. Trả lỗi rõ nếu thiếu cấu hình/phiên hoặc worker chưa có; không tự tạo tác vụ giả thành công.
Brand bổ sung facebookGroupUrl. Danh sách bài đọc bảng Documents kind=articles, lọc brand, tìm kiếm và trạng thái Clef (recommended/rejected/pending), hiển thị số lượng theo bộ lọc. Dữ liệu bài chỉ cho worker tạo; API generic không cho browser tự thêm article/draft/run.
Chọn Repost tạo draft giữ nội dung và media gốc. Chọn Rewrite gọi Bonsai thật qua khóa AI chung, lưu draft chờ duyệt. Xem trước sửa nội dung, chọn tài khoản đăng và duyệt. API duyệt kiểm tra file media local có tồn tại, có nhóm đích /groups/, tài khoản role=publish đang bật và có phiên. Sau duyệt trạng thái approved_pending_worker, lưu bản chụp nhóm đích; chưa đăng lên Facebook và chưa xác minh quyền nhóm. Duyệt lặp bị từ chối. Kho media ở Docker volume sammicrawl_sammicrawl_media.
Worker cào/tải media/Clef nền/publish vẫn CHƯA triển khai. Không có dữ liệu mẫu giữ lại; smoke test đã dọn riêng dữ liệu thử, không thay dữ liệu người dùng.

## Kiểm tra phiên Facebook thật và lỗi runtime

Khi tạo tài khoản hoặc thay cookie, backend kiểm tra dữ liệu cookie rồi dùng Chromium/Playwright mở https://www.facebook.com/me trong context riêng. Hỗ trợ chuỗi Cookie hoặc JSON export, chỉ nạp cookie thuộc facebook.com. Không lưu ảnh chụp hoặc HTML, không ghi cookie vào log, không xử lý CAPTCHA/checkpoint. Chỉ ghi active khi có dấu hiệu giao diện đã đăng nhập; phản hồi không rõ, lỗi mạng/timeout giữ trạng thái chưa xác minh, không suy đoán khóa tài khoản. Kiểm tra live không chứng minh quyền đăng lên nhóm/Page cụ thể.
Nút Kiểm tra Facebook gọi POST /api/accounts/{id}/check; bảng có lý do, mã lỗi và thời điểm. Trạng thái gồm active/locked/checkpoint/expired/restricted/invalid_cookie/unknown. Không sửa trạng thái thủ công. Lỗi INVALID_COOKIE có nghĩa thiếu c_user/xs hoặc định dạng không hợp lệ, không phải tài khoản bị Facebook khóa.
POST /api/accounts/{id}/runtime-error dành cho worker, yêu cầu Bearer WORKER_API_KEY trong .env, body {jobId, operation: crawl|publish, code}. Chỉ nhận job đã lưu và tài khoản được phân công trong job/snapshot. Code LOGIN_REQUIRED/ACCOUNT_LOCKED/CHECKPOINT/RATE_LIMITED cập nhật tình trạng tài khoản và tạm dừng job; PERMISSION_DENIED/NETWORK_ERROR/UNKNOWN_ERROR ghi lý do lỗi, giữ tình trạng Facebook trước đó. Không tự retry hoặc chuyển tài khoản để tiếp tục lỗi chưa xử lý.
Worker cào/publish vẫn chưa triển khai nên chưa có báo lỗi từ tác vụ Facebook thật; đầu nhận và lưu lỗi đã sẵn sàng, cần nối worker khi triển khai. Kiểm tra trạng thái đăng nhập đã hoạt động độc lập. Tài khoản có trạng thái khóa/checkpoint/hết phiên/giới hạn/cookie lỗi được loại khỏi danh sách cào đủ điều kiện.

## Chuyển PostgreSQL (23/10/2026)

Đã chuyển Documents, Users và Logs từ SQL Server, giữ ID, chuỗi JSON và cookie đã mã hóa. Thời gian chuẩn hóa UTC tới microsecond theo PostgreSQL. Công cụ `--migrate-postgres` chỉ chạy với DB đích trống, đối chiếu hash trước commit và từ chối ghi đè dữ liệu. Bí mật ở `.env`, không commit. Sao lưu nguồn và khóa giải mã nằm trong `work/db-migration/`; không xóa volume khóa. Backend giữ provider SQL Server phục vụ rollback.
