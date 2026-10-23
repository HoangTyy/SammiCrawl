# SammiCrawl local

React + ASP.NET Core 10 + PostgreSQL. Docker app mở tại http://localhost:5080.

## Chạy bằng Docker

1. Copy `.env.example` thành `.env`, điền connection string PostgreSQL và hai khóa API ngẫu nhiên.
2. Chạy `docker compose up -d --build`.
3. Mặc định localhost bỏ qua login theo yêu cầu phát triển. Muốn kiểm tra đăng nhập, thêm `SKIP_LOGIN=false` vào `.env` rồi chạy `docker compose up -d app`.

`.env`, `backend/appsettings.Local.json` và thư mục `work/` không được commit hoặc copy vào image Docker. Không chạy `docker compose down -v` nếu cần giữ khóa giải mã cookie và media.

## Connection string và chạy trong IDE

`backend/appsettings.json` chứa provider, host, port, database, username và mẫu connection string. Mật khẩu thật được ghi đè bằng một trong hai cách:

- Docker: `POSTGRES_CONNECTION` trong `.env` được truyền vào `ConnectionStrings__Database`.
- IDE: tạo `backend/appsettings.Local.json` với cấu trúc `ConnectionStrings:Database` giống appsettings.json, điền mật khẩu thật. File này bị Git và Docker bỏ qua. Máy phát triển hiện tại đã có file local này.

Cấu hình được đọc theo thứ tự appsettings mặc định → appsettings.Local.json → biến môi trường. Khi đổi mật khẩu DB, cập nhật cả `.env` và file local nếu sử dụng hai cách chạy.

TLS dùng VerifyCA với chứng chỉ ghim `config/postgres/server.crt` (Docker) hoặc `backend/config/postgres/server.crt` (IDE). Khi nhà cung cấp đổi chứng chỉ, xác minh và cập nhật cả hai bản. Bản build/publish backend có copy chứng chỉ. SQL Server cũ nằm trong profile `legacy-sql`, volume còn giữ để khôi phục; web mặc định dùng PostgreSQL schema `sammicrawl`.

## Tài khoản mẫu

Tạo thủ công một lần trên database đang cấu hình:

```powershell
docker compose run --rm --no-deps app --seed-demo
```

Hoặc trong backend bằng .NET 10 SDK: `dotnet run -- --seed-demo`.

- Email: `demo@sammicrawl.local`
- Mật khẩu mẫu: `SammiDemo@2026`

Đây là tài khoản thử công khai; chỉ tạo trong môi trường thử nghiệm. Lệnh không tự chạy khi khởi động và không reset tài khoản đã tồn tại. Có thể ghi đè `DemoAccount:Email`/`DemoAccount:Password` trong cấu hình local hoặc biến môi trường trước khi seed. Cần xóa hoặc đổi tài khoản mẫu trước khi mở public. Không chỉnh sửa tài khoản có sẵn của người dùng khi seed.

Không có đăng ký công khai. `/api/auth/setup` chỉ tạo quản trị viên đầu tiên khi DB chưa có user; sau đó trả HTTP 409. `/api/auth/login` và `/api/auth/logout` dùng cookie HttpOnly. Bỏ qua login chỉ áp dụng localhost không có header proxy; `appsettings.json` mặc định tắt bypass ngoài cấu hình Docker local.

## Kiến trúc backend

- `Controllers/`: route, binding request, authorization và chuyển kết quả sang HTTP.
- `Services/`: nghiệp vụ Auth, Records, Accounts, Workflows, Content, System; kiểm tra Facebook và khóa AI.
- `Repositories/`: `IProjectRepository`/`ProjectRepository`, toàn bộ truy vấn EF, lưu dữ liệu và reload; scoped cùng DbContext cho mỗi request.
- `Data/Store.cs`: mapping EF Core.
- `Models/`: Document, ProjectUser, Audit và loại bản ghi.
- `Contracts/`: request DTO và kết quả nghiệp vụ.
- `Infrastructure/`: middleware xác thực local/bridge/worker, xử lý lỗi và công cụ chuyển dữ liệu SQL Server → PostgreSQL.
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

Bản SQL Server, `.env` và khóa giải mã trước chuyển đổi nằm trong `work/db-migration/` trên máy phát triển. Công cụ `--migrate-postgres` chỉ cho DB đích trống, so sánh hash sau chuẩn hóa UTC/microsecond rồi mới commit. Không đưa bản sao lưu hoặc mật khẩu lên GitHub.
