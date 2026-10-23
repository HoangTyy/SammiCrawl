# SammiCrawl

React + ASP.NET Core 10 + PostgreSQL. Mã nguồn local và GitHub dùng cùng cấu hình, cùng màn hình đăng nhập, cùng database.

## Chạy

```powershell
docker compose up -d --build
```

Mở http://localhost:5080 và đăng nhập. Không có chế độ tự bỏ qua đăng nhập, không có appsettings.Local.json hoặc connection string riêng cho máy phát triển.

`backend/appsettings.json` chứa connection string PostgreSQL đầy đủ. Docker và chạy backend bằng .NET 10 SDK đều đọc cấu hình này. Khi đổi mật khẩu DB, cập nhật file rồi build/triển khai lại. Theo cơ chế chuẩn ASP.NET Core, bên triển khai vẫn có thể ghi đè cấu hình bằng biến môi trường nếu cần; compose mặc định không ghi đè connection string.

TLS dùng VerifyCA, chứng chỉ duy nhất ở `backend/config/postgres/server.crt`, được copy khi publish và dùng chung trong Docker. Khi nhà cung cấp thay chứng chỉ, xác minh trước khi cập nhật. PostgreSQL schema `sammicrawl`; SQL Server cũ giữ volume và chỉ chạy với profile `legacy-sql`.

`.env` chỉ dùng cho khóa API worker/bridge và mật khẩu SQL Server cũ. Có thể copy `.env.example` để cấu hình các tính năng này. Không có khóa thì web vẫn chạy; bridge và worker không chấp nhận request xác thực bằng khóa trống. Không chạy `docker compose down -v` nếu cần giữ khóa giải mã cookie và media.

## Tài khoản mẫu

Đã tạo trên database hiện tại:

- Email: `demo@sammicrawl.local`
- Mật khẩu mẫu: `SammiDemo@2026`

Nếu sử dụng database mới, tạo một lần sau khi cấu hình connection:

```powershell
docker compose run --rm --no-deps app --seed-demo
```

Hoặc tại thư mục backend bằng .NET 10 SDK: `dotnet run -- --seed-demo`.

Đây là tài khoản thử công khai. Lệnh seed không tự chạy khi khởi động và không reset tài khoản đã tồn tại. Cần xóa hoặc đổi tài khoản mẫu trước khi mở public.

Không có đăng ký công khai. `/api/auth/setup` chỉ tạo quản trị viên đầu tiên khi DB chưa có user; sau đó trả HTTP 409. `/api/auth/login` và `/api/auth/logout` dùng cookie HttpOnly. Localhost cũng bắt buộc đăng nhập như các bản triển khai khác.

## Kiến trúc backend

- `Controllers/`: route, binding request, authorization và chuyển kết quả sang HTTP.
- `Services/`: nghiệp vụ Auth, Records, Accounts, Workflows, Content, System; kiểm tra Facebook và khóa AI.
- `Repositories/`: `IProjectRepository`/`ProjectRepository`, toàn bộ truy vấn EF, lưu dữ liệu và reload; scoped cùng DbContext cho mỗi request.
- `Data/Store.cs`: mapping EF Core.
- `Models/`: Document, ProjectUser, Audit và loại bản ghi.
- `Contracts/`: request DTO và kết quả nghiệp vụ.
- `Infrastructure/`: middleware xác thực bridge/worker, xử lý lỗi và công cụ chuyển dữ liệu SQL Server → PostgreSQL.
- `Program.cs`: cấu hình, đăng ký DI và khởi động; không chứa nghiệp vụ API.

URL API giữ nguyên để frontend tiếp tục sử dụng. Bí mật Facebook được mã hóa bằng Data Protection, không trả cookie đã lưu về browser. Giữ volume `sammicrawl_keys` khi chuyển host để giải mã phiên cũ.

## Khả năng hiện tại

Đã có quản lý thương hiệu/nhóm đích, tài khoản Facebook theo vai trò cào/đăng, sửa/xóa/khôi phục hồ sơ, cấu hình và nhật ký trong PostgreSQL. Kiểm tra Facebook thật bằng Chromium/Playwright khi thêm tài khoản hoặc thay cookie; báo mã lỗi và thời điểm. Cookie sai định dạng trả `invalid_cookie`, không kết luận tài khoản bị khóa. Không vượt CAPTCHA/checkpoint.

Studio tự chọn toàn bộ tài khoản cào đang bật và đủ điều kiện, nhận giới hạn bài và khoảng nghỉ min/max. Thư viện bài lọc theo brand và trạng thái Clef. Repost tạo bản nháp giữ nội dung/media gốc; Rewrite gọi Bonsai thật, rồi chờ duyệt. Duyệt kiểm tra file media và tài khoản đăng, lưu trạng thái `approved_pending_worker`.

Worker cào Facebook, tải/kiểm chứng media, Clef lọc nền và đăng Facebook vẫn **chưa triển khai**. API chặn chạy và báo lý do; không báo thành công giả. Thư viện chưa có bài thu thập thật.

`POST /api/accounts/{id}/runtime-error` yêu cầu Bearer `WORKER_API_KEY`, kiểm tra job/account binding và lưu lỗi; không tự retry. `POST /api/ai/rewrite` dùng Bonsai qua Ollama, singleton semaphore chỉ cho một request AI, unload model trước và sau suy luận. Không chạy thêm replica hoặc tác vụ ngoài ứng dụng nếu cần độc quyền toàn máy.

## Bridge LLM cho backend host

`GET /bridge/health`, `POST /bridge/rewrite` yêu cầu Bearer `REMOTE_AI_KEY`. Cloudflare Tunnel có thể trỏ `ollama.khanhdc.id.vn` tới app cổng 5080; không trỏ thẳng Ollama 11434. Hostname/tunnel public chưa cấu hình hoặc xác minh trong dự án này. Giữ rate limit và bảo vệ truy cập khi triển khai public.

## Sao lưu và chuyển dữ liệu

Bản SQL Server, `.env` và khóa giải mã trước chuyển đổi nằm trong `work/db-migration/` trên máy phát triển. Công cụ `--migrate-postgres` chỉ cho DB đích trống, so sánh hash sau chuẩn hóa UTC/microsecond rồi mới commit. Bản sao lưu và khóa worker/bridge không đưa lên GitHub. Connection string PostgreSQL được lưu chung trong appsettings.json theo cấu hình dự án; khi reset mật khẩu DB cần sửa file này và triển khai lại.
