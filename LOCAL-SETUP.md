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

URL API giữ nguyên để frontend tiếp tục sử dụng. Bí mật Facebook được mã hóa bằng Data Protection, chỉ trả cookie đã lưu khi người dùng đăng nhập dự án chủ động bấm Hiện cookie; response no-store. Giữ volume `sammicrawl_keys` khi chuyển host để giải mã phiên cũ.

## Khả năng hiện tại

Đã có quản lý thương hiệu/nhóm đích, tài khoản Facebook theo vai trò cào/đăng, sửa/xóa/khôi phục hồ sơ, cấu hình và nhật ký trong PostgreSQL. Kiểm tra Facebook thật bằng Chromium/Playwright khi thêm tài khoản hoặc thay cookie; báo mã lỗi và thời điểm. Cookie sai định dạng trả `invalid_cookie`, không kết luận tài khoản bị khóa. Không vượt CAPTCHA/checkpoint.

Studio tự chọn toàn bộ tài khoản cào đang bật và đủ điều kiện, nhận giới hạn bài và khoảng nghỉ min/max. Thư viện bài lọc theo brand và trạng thái Clef. Repost tạo bản nháp giữ nội dung/media gốc; Rewrite gọi Bonsai thật, rồi chờ duyệt. Duyệt kiểm tra file media và tài khoản đăng, lưu trạng thái `approved_pending_worker`.

Worker cào local đã nối vào nút Chạy cào bài; nhận bài/ảnh nguồn, chống trùng và đánh giá Clef tuần tự. Worker đăng Facebook chưa triển khai. Xem giới hạn worker bên dưới.

`POST /api/accounts/{id}/runtime-error` yêu cầu Bearer `WORKER_API_KEY`, kiểm tra job/account binding và lưu lỗi; không tự retry. `POST /api/ai/rewrite` dùng Bonsai qua Ollama, singleton semaphore chỉ cho một request AI, unload model trước và sau suy luận. Không chạy thêm replica hoặc tác vụ ngoài ứng dụng nếu cần độc quyền toàn máy.

## Bridge LLM cho backend host

Gateway AI có health/rewrite/evaluate, xác thực Bearer và cổng riêng 5081; không trỏ tunnel tới 5080 hoặc Ollama 11434. Client dùng RemoteAi:BaseUrl và RemoteAi:ApiKey. Xem REMOTE-AI.md; hostname ollama.khanhdc.id.vn đã được cấu hình và kiểm tra HTTPS ngày 24/10/2026.

## Sao lưu và chuyển dữ liệu

Bản SQL Server, `.env` và khóa giải mã trước chuyển đổi nằm trong `work/db-migration/` trên máy phát triển. Công cụ `--migrate-postgres` chỉ cho DB đích trống, so sánh hash sau chuẩn hóa UTC/microsecond rồi mới commit. Bản sao lưu và khóa worker/bridge không đưa lên GitHub. Connection string PostgreSQL được lưu chung trong appsettings.json theo cấu hình dự án; khi reset mật khẩu DB cần sửa file này và triển khai lại.

## Hồ sơ và workflow theo StuXP

Hồ sơ có danh sách kênh Group/Page, mỗi kênh có ID ổn định, URL, chỉ tiêu/ngày, đối tượng, tone, yêu cầu nội dung/media, giới hạn từ và quy định nhận diện. Brand giữ các chủ đề được phép, nội dung cấm và checklist dùng chung. Dữ liệu Group cũ vẫn đọc qua facebookGroupUrl nếu chưa có channels.

Database đã nạp 8 thương hiệu và 14 kênh từ workbook StuXP theo yêu cầu. Mức tối thiểu các Group cộng 50 bài/ngày; Page chưa ghi chỉ tiêu trong nguồn giữ 0 (chưa đặt). Group hub áp dụng 100–250 từ theo yêu cầu riêng; Group khác 80–250, Page 50–150. Dữ liệu này ở PostgreSQL, không tự seed mỗi khi khởi động. Không đưa nội dung trả công CTV vào nghiệp vụ hiện tại.

POST /api/articles/{id}/evaluate gọi clef-flash:latest qua /v1/systemone, hỗ trợ tối đa 4 ảnh local JPG/PNG/WebP (mỗi ảnh 5 MB), lưu quyết định có cấu trúc, điểm phù hợp, chủ đề, lý do lựa chọn và cảnh báo. Điểm 0–100 quy đổi từ thang phù hợp 0–4, không phải xác suất bài đúng. Clef là decision model, không sinh đoạn giải thích tự do. Video và media không phân tích được sẽ yêu cầu xem thủ công. Lỗi AI giữ kết quả cũ. Có thể gọi thủ công hoặc worker gọi tuần tự sau khi thu thập.

Khi Repost/Rewrite phải chọn một kênh đang bật. Bonsai nhận profile và hướng dẫn riêng của kênh. Draft giữ bản chụp profile, kênh, nguồn và media; sửa nội dung, checklist và giờ dự kiến tại PUT /api/drafts/{id}. Bản nháp lưu xong có thể mở lại trong Danh sách bài → Bản nháp & lịch đăng. Thay nội dung trên UI sẽ bỏ chọn checklist để duyệt lại.

Duyệt kiểm tra giới hạn từ, checklist bắt buộc, file media có thật, kênh còn hoạt động/không đổi URL và tài khoản đăng đã xác minh active. Lưu người duyệt, giờ duyệt và giờ dự kiến (UTC; UI hiển thị Việt Nam). Sau duyệt chỉ là approved_pending_worker, không đăng Facebook. Không có scheduler hoặc worker tự chạy lịch trong bản này.

GET /api/reports/content?date=yyyy-MM-dd thống kê từng kênh và lịch dự kiến theo ngày Việt Nam. Số thu thập/chờ duyệt là tổng hiện có; đã đăng/còn thiếu theo ngày chọn. Chỉ đếm published khi có URL bài Facebook và thời gian đăng không ở tương lai. Duyệt và đặt giờ không được đếm là đã đăng. Kiểm thử chỉ dùng dữ liệu nội bộ, không đăng bài thật.

## Quản lý kênh đăng riêng

Menu Kênh đăng đọc chung channels trong hồ sơ thương hiệu, không nhân bản kênh. Có tìm kiếm/lọc thương hiệu, Group/Page, trạng thái; thêm/sửa/ngưng/bật lại và gán tài khoản role=publish đang được bật. Tình trạng Facebook của tài khoản hiển thị riêng với quyền đăng trên kênh (chưa xác minh). Không có thao tác đăng bài hoặc kiểm tra quyền Facebook trong trang này.

GET /api/channels; POST /api/brands/{brandId}/channels; PUT /api/brands/{brandId}/channels/{channelId}. Kênh dùng ID ổn định; ngưng không xóa lịch sử. Tài khoản phụ trách trở thành lựa chọn mặc định trong bản nháp mới, người duyệt vẫn có thể chọn tài khoản đăng hợp lệ khác. Lưu hồ sơ/kênh dùng kiểm tra phiên bản và ghi có điều kiện để tránh ghi đè thay đổi đồng thời. Dữ liệu 14 kênh hiện có được giữ nguyên, không tự gán tài khoản.

## Kết nối tài khoản Facebook qua backend

POST /api/accounts/{id}/login (đã đăng nhập dự án), body email/password, thử đăng nhập trong Chromium của backend rồi lưu cookie bằng Data Protection nếu Facebook cấp phiên và UID khớp. Mật khẩu không được lưu. Kết quả phải qua kiểm tra phiên thật; checkpoint/CAPTCHA không được tự giải. Phiên trình duyệt cá nhân không tự đồng bộ vào container. Endpoint này không cung cấp worker cào/đăng.

Form tài khoản nhập riêng UID và giá trị xs; frontend gửi trường xs, backend tự ghép c_user=UID; xs=giá trị trước khi mã hóa/kiểm tra. API cookie cũ vẫn tương thích. Endpoint session trả thêm xs để form hiển thị dữ liệu cũ dạng chuỗi hoặc JSON. Không giải mã URL giá trị xs.

## Chống trùng bài nguồn

POST /api/articles/ingest (yêu cầu đăng nhập) là điểm nhận metadata/nội dung bài cho luồng thu thập, không tự cào Facebook. Nhận brandId/sourceUrl/content/title. Link phải nhận diện được bài cụ thể; media không rỗng bị từ chối vì đường ingest này chưa tải media. Trả article/duplicate/duplicateReason. Bài trùng trả bản đã có, không ghi đè nội dung hoặc kết quả AI. So sánh theo thương hiệu, ID bài gốc (bỏ khác biệt query tracking/host) hoặc SHA256 của Unicode NFKC, gộp khoảng trắng, không phân biệt hoa/thường. Bài chỉ có media/không chữ không bị gộp theo nội dung rỗng. Không lọc gần nghĩa hoặc nội dung viết lại. Khóa transaction trong DB bảo vệ lookup/insert qua nhiều request/replica. Dữ liệu cũ cũng được so sánh; không xóa bản trùng cũ tự động. Hiện quét các bài đã lưu trong transaction nên cần bảng chỉ mục/hash riêng khi quy mô tăng. Worker cào local dùng cùng cơ chế repository này để lưu cả metadata và file media đã tải; endpoint ingest thủ công vẫn chỉ nhận metadata.

## Worker cào Facebook local (24/10/2026)

POST /api/crawl/start nhận cấu hình từ UI và tạo task pending, chuyển cho BackgroundService dùng Chromium trong container. Chỉ tài khoản role=crawl, enabled, facebookStatus=active, không ở thùng rác và có phiên mã hóa được chọn. Một lượt mỗi process; không triển khai nhiều replica crawler vì queue hiện in-memory. Restart chuyển task dang dở thành paused, không tự retry. Nút hủy chuyển task paused, worker kiểm tra giữa các bước. Dừng/challenge/login/rate limit không được tự vượt hoặc thử lặp lại.

Giới hạn bài là số bài mới được lưu (bài trùng không tính). delayMinMs/delayMaxMs hiện là thời gian nghỉ giữa bài, kể cả bài trùng; nghỉ cuộn nội bộ 1,5–3 giây. accountPostLimit và accountRestMinMs/accountRestMaxMs áp dụng giữa các tài khoản. Chỉ nhận minLikes/minComments=0 và includeComments=false trong bản đầu này, không giả lập tương tác/bình luận chưa lấy. keywords lọc OR theo các mục cách nhau bằng dấu phẩy; topicDescription bổ sung yêu cầu chủ đề khi Clef đánh giá cùng quy chuẩn thương hiệu.

Nguồn: đọc DOM bài được Facebook hiển thị, mở Xem thêm, nhận diện permalink/ID từ link bài hoặc gm ID của ảnh. Tải ảnh/video qua URL HTTPS trên CDN Facebook vào volume media. Đây là bản media Facebook cung cấp trên feed, không bảo đảm độ phân giải gốc; video blob/DASH chưa được ghép/tải. Ghi mediaWarnings và ép bài có media chưa xác minh sang cần xem lại khi Clef đề xuất. Không báo đã tải video khi chỉ có thumbnail. Sau thu thập, Clef chạy từng bài qua khóa model hiện có. Lỗi AI giữ pending để đánh giá lại, không xóa bài.

Nếu Facebook đổi DOM có thể không nhận diện được bài; task lưu lý do, số bài thực tế và lượt trùng. Max scroll dừng sớm có thể dẫn đến paused/SOURCE_EXHAUSTED, không coi đủ số yêu cầu. Không có thao tác đăng trong worker này.
