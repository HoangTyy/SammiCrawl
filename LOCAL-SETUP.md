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

## Bảo mật API từ 25/10/2026

Frontend tự gọi GET `/api/auth/csrf` rồi gửi header `X-CSRF-TOKEN` cho POST/PUT/PATCH/DELETE dưới `/api`, kể cả login/setup/logout. Client thủ công phải giữ cùng cookiejar, lấy token mới sau khi login đổi identity. Missing/invalid token trả403 `CSRF_TOKEN_INVALID`; không tự retry mutation. `/bridge/*` vẫn Bearer RemoteAiKey; `/api/accounts/{id}/runtime-error` chỉ miễn CSRF khi đúng WorkerKey đã xác thực. Cookie dự án không thay được key của hai nhóm này.

Login/setup giới hạn10request/phút theo IPpeer thực, không tin X-Forwarded-For tự khai, body16KiB; email<=254/password<=1024. Vượt quota trả429+Retry-After. Ticketcookie mới8h, không sliding; không tự thu hồi ticketcũ. SameSiteStrict/HttpOnly giữ nguyên. HTTPSrequest có Securecookie, HTTPloopback vẫn dùng được; trustedproxy/TLS phải cấu hình riêng theo deployment, không bật trust-any-proxy.

`/media/*` cần sessioncookie đã đăng nhập, anonymous401, khôngcache; publicprovider chặn media canonical khỏi kho staticpublic. AssetsJS/CSS/login vẫnpublic. Missingmedia404, rangevideo vẫn được hỗ trợ. Tảimedia dùngHTTPS443 allowlist, khôngredirect, scopedcookie, streamtốiđa50MB, timeout30s, filepartial không được côngbố thành mediahoàntất.

Test độc lập: xem `tests/SecurityChecks/README.md`. Báo cáo và giớihạn: `docs/research/SECURITY-HARDENING.md`. Không đổi credential hoặc resetdemo khi chạy các kiểmtra này.

## Đồng bộ trực tiếp (28/10/2026)

Khi nhiều người mở cùng backend, SignalR `/api/live` thông báo sau khi ứng dụng lưu thay đổi thành công, các danh sách/báo cáo tự tải lại. Thanh trạng thái hiển thị kết nối đồng bộ; mất kết nối tự thử lại và tải lại dữ liệu khi nối lại. Biểu mẫu đang mở giữ nội dung nhập và báo cần đối chiếu khi danh sách được cập nhật. Kết nối dùng phiên cookie hiện có và CSRF cho POST; không mở CORS hoặc truy cập hub bằng khóa bridge/worker.

Không có polling DB: SQL trực tiếp hoặc ghi từ backend khác không tự đẩy thông báo. Có thể bấm Làm mới hoặc quay lại cửa sổ để tải dữ liệu. Cấu hình hiện hỗ trợ một backend; nhiều replica cần backplane. Đồng bộ hiển thị không bảo đảm chống ghi đè khi cùng sửa: conditional update brand/kênh được giữ, các loại khác chưa có bảo vệ tương đương.

Vite dev proxy `/api` có WebSocket (`ws: true`), giữ Host (`changeOrigin: false`) để kiểm tra Origin cùng nguồn. Khi triển khai qua reverse proxy phải hỗ trợ WebSocket và cấu hình Host/TLS đúng môi trường.

Giới hạn tác vụ cào: mỗi tài khoản chỉ được dùng một lượt; tổng maxPosts phải <= min(1000, số tài khoản cào đủ điều kiện × accountPostLimit). Form hiển thị sức chứa và chặn chạy nếu vượt; API kiểm tra lại danh sách tài khoản thực trước khi tạo task, trả 400 CRAWL_CAPACITY_EXCEEDED. Không tự hạ thông số hoặc quay vòng tài khoản. Mẫu cấu hình có thể lưu để tái sử dụng, nhưng luôn kiểm tra lại sức chứa khi chạy; trần này không bảo đảm lấy đủ bài nếu nguồn ít, trùng bài hoặc lỗi phiên.

## Cào và đánh giá Clef — 28/10/2026

Worker đánh giá Clef tuần tự ngay sau mỗi bài mới được lưu, trước khi xử lý bài tiếp theo. StartCrawl kiểm tra AI online/có clef-flash:latest/không busy trước khi tạo task (không suy luận và không chứng minh endpoint systemone hoạt động). Giữ singleton gate, không chạy song song model. Nếu đánh giá lỗi/busy/kết quả không hợp lệ, dừng lượt ở paused/CLEF_EVALUATION_INCOMPLETE, giữ bài pending để đánh giá thủ công, không tự retry Facebook hoặc đổi model.

Task chỉ completed/100% khi đủ số bài thu thập và tất cả có kết quả Clef hợp lệ; running tối đa99%, đủ bài chưa đánh giá là90%. maxPosts và accountPostLimit vẫn tính bài mới thu thập, kể cả bài sau đó bị loại; không tự cào bù số bài không phù hợp. Task lưu clefEvaluatedPosts/clefRecommendedPosts/clefReviewPosts/clefRejectedPosts. Queue hiển thị thống kê và lý do dừng.

ContentService đánh giá rejected thì soft-delete bài với deletedReason=clef_rejected: không còn trong danh sách/báo cáo, không tạo draft; vẫn giữ record/media/kết quả để chống trùng và đối chiếu, không xóa file vật lý. recommended giữ điểm/lý do/chủ đề; review (nhất là media chưa phân tích được) giữ riêng để người duyệt, không tự xem là phù hợp. Draft cũ giữ snapshot, không xóa theo bài nguồn. Không tự áp dụng lại đánh giá cho bài lịch sử.

Khi khởi động, các task Facebook local cũ completed nhưng còn bài gắn crawlTaskId chưa đánh giá được chuyển paused/CLEF_EVALUATION_INCOMPLETE; không replay. Không suy ra task thiếu mọi bài nguồn (đã bị xóa) là lỗi. Khi đánh giá lại thủ công, thống kê task cập nhật; chỉ tự hoàn tất task tạm dừng vì Clef khi đủ bài và đủ kết quả, không tiếp tục cào. Các tác vụ tạm dừng do người dùng không tự resume.

## Gateway gián đoạn — 29/10/2026

Client chỉ thử lại tối đa một lần (nghỉ2giây) với POST bridge/evaluate thuần suy luận khi nhận HTTP502/504/520/522/524 chưa có mã lỗi xác định. Tổng2lần gọi, giữ chung gate; không retry409 busy/429 rate limit/auth/lỗi output Clef đã xác định, không tự retry rewrite/DB mutation hoặc Facebook. Hết lần thử vẫn lỗi thì giữ pending, task paused và không báo100%.

Gateway mới trả mã lỗi phân biệt CLEF_INVALID_OUTPUT, AI_UPSTREAM_FAILED, AI_TIMEOUT và log code/type/trace, không log payload/cookie/key. Client hiển thị mã/lý do an toàn thay vì che mọi lỗi bằng một câu chung. Cần pull và build/recreate app trên cả máy client lẫn máy chủ AI; máy AI phải giữ RemoteAi:BaseUrl trống để không gọi vòng. Giữ .env và volume khóa/media trên mỗi máy; file env thiếu dấu chấm được ignore, Compose vẫn cần .env.

Đánh giá lại hết các bài của tác vụ dừng vì Clef: nếu đã đủ mục tiêu thì completed; nếu mới thu thập4/10 thì vẫn paused/COLLECTION_INCOMPLETE, báo còn thiếu bài thu thập thay vì ghi Clef còn0bài chờ. Không tự resume Facebook.

## Tìm nhóm theo chủ đề — 29/10/2026

CrawlerStudio có sourceMode=group-search (mặc định) và url (tương thích). Từ khóa lấy allowedTopics/name thương hiệu, sửa được trước chạy, tối đa6 mục/200ký tự; maxGroups1–20 và groupPostLimit1–1000. Tổng mục tiêu không vượt min(1000, tài khoản×accountPostLimit, nhóm×groupPostLimit). Mẫu cũ không sourceMode áp dụng url; tạo nhanh bằng URL cũng chọn url. Không tự tăng/hạ thông số.

Worker dùng phiên tài khoản cào để mở Facebook search/groups, đọc các link nhóm hiển thị, canonical hóa URL và giới hạn tối đa3 lần cuộn/từ khóa. Không tự tham gia nhóm, không vượt checkpoint/giới hạn; chỉ đọc nội dung tài khoản truy cập được. maxGroups/groupPostLimit là giới hạn chung trong tác vụ; accountPostLimit áp dụng tổng bài của tài khoản qua các nhóm. Hết nguồn/tìm không thấy đủ bài vẫn paused, không báo đủ mục tiêu.

Nhóm nguồn lưu Document Kind=sourceGroups (không thêm generic CRUD), ID ổn định theo brand+URL; repository upsert dưới transaction/PG advisory lock. GET /api/source-groups qua service trả tên/link/từ khóa, số bài đã thu thập, accepted/review/rejected/pending và kênh phù hợp. Bảng nằm dưới form Cào bài, giữ NavigationTab/menu. Số accepted chỉ tính aiStatus recommended và không deleted; review không tính đã chấp nhận. Thống kê dựa trên các bài hiện lưu; không tự gắn nguồn cho bài cũ hoặc nhân bản bài trùng.

Bài từ group-search lưu sourceGroupId/name/url và channelEvaluationRequired. Sau đánh giá thương hiệu, Clef đối chiếu lần lượt các kênh đang bật trong cùng gate bằng protocol Evaluate hiện có (không cần đổi gateway). Lưu aiChannelMatches (ID/tên/status/score/reason) và aiChannelEvaluationStatus=complete chỉ khi xong. Mọi kênh rejected thì loại bài; chỉ review thì giữ review. Bài brand rejected bỏ qua đối chiếu kênh. Khi kênh/AI lỗi, bài pending và tác vụ không hoàn thành. Có thêm nhiều request Clef nên thời gian/khả năng gặp giới hạn tăng. Kênh phù hợp là gợi ý dựa vào các bài đã đánh giá, không chứng minh quyền đăng hoặc toàn bộ nhóm phù hợp.

Chưa chạy thử tìm/cào Facebook thật trong lượt triển khai; DOM Facebook có thể thay đổi. Kiểm tra build, helper và UI dùng fixture không thay dữ liệu thật; chưa bảo đảm chất lượng kết quả tìm nhóm hoặc đánh giá AI trên dữ liệu thật.

## Rewrite dùng ảnh làm ngữ cảnh — 29/10/2026

Bấm Rewrite gửi phần chữ bài gốc cùng tối đa4 ảnh JPG/PNG/WebP local (5 MB/ảnh) cho Bonsai trong một request. AI chỉ viết lại caption, dùng ảnh để tránh lạc đề, không trả mô tả/OCR hoặc bắt người dùng xác nhận ghi chú ảnh. Giữ media gốc trong draft; thiếu file/video chưa phân tích vẫn báo lý do, không âm thầm bỏ ảnh. UI có tiến độ ước tính, đóng/cuộn được khi chờ; cần duyệt nội dung trước khi đăng. Draft lịch sử giữ nguyên. Gateway hiện có đã hỗ trợ Images và Instructions; chưa kiểm chứng chất lượng diễn giải trên mọi ảnh.

## Metadata tương tác bài nguồn — 29/10/2026

FacebookCrawlWorker đọc bộ đếm hiển thị trong phạm vi bài bằng FacebookPostMetadata.Script, không bấm cảm xúc/bình luận, không gọi API Facebook riêng. Lưu reactions (tổng cảm xúc), likes chỉ khi có nhãn Thích rõ ràng, comments, raw CountText và CountApproximate cho số rút gọn, interactionCapturedAt UTC. Thiếu/ẩn không coi là0; loại số trong caption, comment lồng, phần tử ẩn và container lẫn nhiều bài. Chưa hỗ trợ bộ lọc tương tác, không tải nội dung bình luận. DOM khác chưa nhận diện có thể để trống.

Giờ nguồn lưu sourcePublishedAt UTC chỉ từ data-utime hoặc ISO datetime có múi giờ hợp lệ; thiếu thì giữ sourcePublishedText, không đoán giờ tuyệt đối từ nhãn tương đối. createdAt vẫn là thời gian lưu/thu thập. UI phân biệt Thu thập / Đăng gốc, raw nhãn hiển thị Facebook lúc cào; số K/M/nghìn/triệu có dấu≈. Bài đã lưu trước thay đổi không tự backfill và bài trùng không bị sửa metadata; áp dụng cho bài mới thu thập.

Kiểm tra độc lập tests/FacebookMetadataChecks dùng Chromium thật với HTML fixture và code mapper production, chặn toàn bộ mạng; không đọc DB/phiên Facebook/Ollama. Chưa chạy thử Facebook thật trong lượt bổ sung; fixture pass không bảo đảm mọi layout Facebook đều đọc được.
