# SammiCrawl — hướng dẫn cho AI và người tiếp quản

Cập nhật: 25/10/2026. Đọc file này trước khi sửa mã; sau đó đọc `LOCAL-SETUP.md` và các file liên quan đến tác vụ. Đây là mô tả hiện trạng, không phải bằng chứng rằng worker đã hoạt động. Kiểm tra lại source/runtime khi tiếp quản.

## Mục tiêu sản phẩm

Web tiếng Việt hỗ trợ quản lý nội dung Facebook theo thương hiệu. Luồng mong muốn:

1. Chọn thương hiệu, Page nguồn và thông số thu thập; dùng tất cả tài khoản cào đang bật/đủ điều kiện.
2. Thu thập bài và ảnh/video gốc; Clef vision/decision lọc bài ở nền.
3. Trong Bài viết & duyệt đăng, lọc theo thương hiệu/trạng thái Clef rồi chọn kênh đích.
4. Repost = sao chép nội dung và media gốc để đăng lại, không phải share link. Rewrite = Bonsai viết lại nội dung, giữ media gốc.
5. Tạo bản nháp, chỉnh sửa, kiểm tra checklist, chọn tài khoản đăng, duyệt và đặt giờ dự kiến.
6. Worker xuất bản và trả URL/thời điểm đăng thực tế — phần này chưa triển khai.

Không biến giao diện thành wizard nhiều bước. Không tự rewrite/đăng ngay lúc cào. Tài khoản cào và tài khoản đăng có hai vai trò riêng. Khoảng nghỉ min/max là cấu hình ngẫu nhiên, không bảo đảm tránh giới hạn Facebook.

## Kiến trúc và nơi sửa

- `frontend/`: React 19 + TypeScript + Vite; `src/App.tsx` điều hướng bằng state, không phải React Router.
- `frontend/src/components/Navbar.tsx`: menu; `src/types.ts`: NavigationTab.
- `src/components/views/CrawlerStudioView.tsx`: form cào, lưu/chọn/áp dụng cấu hình, localStorage `sammicrawl_crawl`.
- `DataVaultView.tsx`: bài nguồn, bộ lọc Clef, repost/rewrite, duyệt bản nháp và giờ dự kiến.
- `ChannelsView.tsx`: Group/Page theo thương hiệu và tài khoản đăng mặc định.
- `FacebookAccountsView.tsx`: tài khoản, vai trò, trạng thái và lỗi phiên Facebook.
- `WorkspaceView.tsx`: dùng chung màn hình thương hiệu/mẫu cấu hình và chế độ thùng rác.
- `TrashView.tsx`: tab con thương hiệu/mẫu cấu hình/tài khoản Facebook; khôi phục, không có xóa vĩnh viễn.
- `TaskQueueView.tsx`, `DashboardView.tsx`, `components/ContentReport.tsx`: hàng đợi/báo cáo.
- `src/brandPolicy.ts`, `components/BrandProfileEditor.tsx`: hồ sơ và quy chuẩn kênh.
- `src/components/HelpTooltip.tsx`: icon (!) cho hướng dẫn tiêu đề, hiện khi hover/focus. `src/App.css`: CSS hiện tại.
- `backend/`: ASP.NET Core .NET 10. `Controllers` chỉ route/binding/auth; `Services` giữ nghiệp vụ; `Repositories` giữ truy vấn/lưu EF. `Program.cs` chỉ cấu hình/DI/khởi động.
- `backend/Services/BrandPolicy.cs`: kiểm tra hồ sơ/kênh/checklist. `ChannelsService.cs`, `ContentService.cs`, `ContentReportService.cs`: nghiệp vụ tương ứng.
- `backend/Services/AiRunner.cs`: kết nối AI và khóa một model; `FacebookAccountHealth.cs`: kiểm tra phiên Facebook.
- `backend/Data/Store.cs`, `Models/`: EF và dữ liệu. Tìm file bằng tên nếu vị trí thay đổi.

Các đường dẫn frontend viết tắt ở trên đều tính từ `frontend/`.

## Menu hiện tại

Menu hai tầng, giữ nguyên NavigationTab và API:
- Tổng quan → Báo cáo tổng quan.
- Thu thập → Cào bài, Hàng đợi tác vụ, Mẫu cấu hình cào bài.
- Nội dung → Bài viết & duyệt đăng, Kênh đăng.
- Quản lý → Thương hiệu, Tài khoản Facebook.
- Thùng rác → giữ các danh mục khôi phục bên trong.

Chọn nhóm mở màn hình đầu tiên của nhóm, chọn lại nhóm hiện tại không reset màn hình con. Bản nháp lọc theo trạng thái backend thực và kênh snapshot; không bổ sung trạng thái chỉ để giống công cụ tham khảo. Tài liệu nghiên cứu/đối chiếu nguồn: docs/research/SAMMICRAWL-RESEARCH.md.

Không còn tab Nội dung AI hoặc AI Rewriter riêng: quản lý repost/rewrite trong **Bản nháp & lịch đăng** thuộc Bài viết & duyệt đăng. Không đưa lại tên Không gian dự án hoặc hai ô Tổng thương hiệu/Kênh đăng trên trang thương hiệu. Mô tả tiêu đề dùng tooltip (!), không để đoạn hướng dẫn dài ngay dưới tiêu đề.

## Dữ liệu và trạng thái

PostgreSQL hiện là database chính, schema `sammicrawl`. Docker SQL Server chỉ là profile legacy. Dữ liệu gồm ProjectUser, Audit và Document theo Kind (brands, accounts, presets, articles, drafts, rewrites cũ...). Kiểm tra Models/Services trước khi thêm trường.

- Hồ sơ brand chứa `channels`; trang Kênh đăng dùng chung dữ liệu này, không tạo kho kênh thứ hai.
- Kênh có ID ổn định, Group/Page, URL, active, chỉ tiêu/ngày, giới hạn từ, đối tượng/tone/yêu cầu nội dung-media và publishAccountId. Hỗ trợ dữ liệu cũ facebookGroupUrl.
- Gán tài khoản đăng chỉ là mặc định cho bản nháp; sức khỏe tài khoản không chứng minh quyền đăng Group/Page. Quyền đăng hiện chưa xác minh.
- Draft lưu snapshot nguồn/profile/kênh/media. Thay hồ sơ không được âm thầm sửa lịch sử.
- Sửa brand/kênh có kiểm tra phiên bản và conditional update; giữ bảo vệ chống mất thay đổi khi sửa đồng thời.
- `approved_pending_worker` = đã duyệt, đang chờ worker, **không phải đã đăng**.
- Báo cáo chỉ đếm đã đăng khi có URL Facebook và thời điểm hợp lệ; ngày báo cáo theo Việt Nam, lưu thời gian UTC.
- Có 8 thương hiệu/14 kênh nhập từ workbook StuXP ở DB dùng khi phát triển. Đây là dữ liệu DB, không có nghĩa clone repo sẽ tự seed chúng. Không tự tạo lại hoặc ghi đè dữ liệu người dùng.

## Phần chạy thật / phần còn thiếu

Đã có CRUD, đăng nhập, lưu PostgreSQL, kiểm tra tài khoản bằng Playwright, gọi AI thủ công, tạo/sửa/duyệt draft và báo cáo. Clef dùng `clef-flash:latest` qua `/v1/systemone` (decision/vision, không phải chat completion). Bonsai dùng `bonsai-2:27b` để rewrite. Xem AiRunner trước khi đổi protocol.

**Đã có worker cào local** từ `/api/crawl/start`: Channel + BackgroundService, một lượt tại một thời điểm, lấy phiên mã hóa của tài khoản cào active, lưu tiến độ và bài qua chống trùng, tải file media Facebook cung cấp rồi đánh giá Clef tuần tự. Chưa có scheduler/worker đăng. Chưa hỗ trợ bình luận/bộ lọc tương tác; không bảo đảm video blob hoặc chất lượng ảnh gốc. Media thiếu phải cảnh báo. Khi restart, lượt dang dở chuyển paused, không tự replay. Duyệt/đặt lịch không phải published. Xem LOCAL-SETUP.md để biết giới hạn.

Một request AI tại một thời điểm qua singleton gate, unload model trước/sau. Không chạy song song Clef và Bonsai. Khóa trong ứng dụng không bảo đảm độc quyền với chương trình khác hoặc nhiều replica.

## Chạy và kiểm tra

Tại thư mục gốc, có Docker Desktop:

```powershell
docker compose up -d --build
```

Web: http://localhost:5080. PostgreSQL đọc từ `backend/appsettings.json`, TLS dùng `backend/config/postgres/server.crt`. Compose mặc định không dựng DB PostgreSQL local; máy cần truy cập DB đã cấu hình. Ollama từ container dùng `http://host.docker.internal:11434`; máy host phải bật Ollama và có model.

Tài khoản demo công khai: `demo@sammicrawl.local` / `SammiDemo@2026`. Nếu DB mới, sau build chạy `docker compose run --rm --no-deps app --seed-demo`. Không có đăng ký công khai; setup chỉ dành cho user đầu tiên. Local/repo đều bắt buộc login, không tự thêm bypass.

Frontend build: `cd frontend` → `npm ci` → `npm run build`. Backend build: `dotnet build backend/SammiCrawl.Backend.csproj` (xác minh tên csproj thực tế trước khi chạy). Kiểm tra toàn stack bằng `docker compose build app`; triển khai local `docker compose up -d --no-deps app`. Đợi `/api/auth/status` trả 200 rồi kiểm tra UI. `git diff --check` trước khi bàn giao.

Bộ kiểm tra bảo mật độc lập nằm tại `tests/SecurityChecks/`, dùng fixture riêng không kết nối DB, Facebook hoặc Ollama. Các script trong `work/` bị ignore và không có sẵn khi pull. Không sửa/xóa dữ liệu thật để thử. UI pass/build pass không chứng minh Facebook đã cào/đăng thật.

## API để tìm nhanh

Xem Controllers để biết method/body/auth chính xác:

- `/api/auth/status`, `/setup`, `/login`, `/logout` dưới prefix `/api/auth`.
- `/api/records/{kind}`: CRUD/soft-delete; `/{id}/restore`: khôi phục loại được hỗ trợ.
- `/api/channels`; `/api/brands/{brandId}/channels` và `/{channelId}`.
- `/api/crawl/start`.
- `/api/articles/ingest`: nhận bài không media, chống trùng theo thương hiệu + nguồn/nội dung chuẩn hóa, khóa DB trước khi kiểm tra/lưu; trả duplicate và bản đã có. Đây chưa phải worker cào. Xem LOCAL-SETUP.md về phạm vi hiện tại.
- `/api/articles/{id}/evaluate`, `/api/articles/{id}/draft`; `/api/drafts/{id}` và thao tác duyệt trong ContentController.
- `/api/reports/content?date=yyyy-MM-dd`.
- `/api/accounts/{id}/login`: email/password tạm thời, cookie mã hóa khi đăng nhập và kiểm tra thành công; không lưu password, không tự giải xác minh.
- `/api/accounts/{id}/runtime-error`: Bearer WORKER_API_KEY, phải gắn đúng job/account.
- `/bridge/health`, `/bridge/rewrite`: Bearer REMOTE_AI_KEY.

## Quy tắc khi AI tiếp tục làm

1. Không commit/push trừ khi người dùng ra lệnh rõ. Yêu cầu sửa không tự bao gồm quyền push.
2. Hiện người dùng chưa cho phép đăng Facebook thật. Không thử đăng, tạo lịch chạy thật hoặc vượt CAPTCHA/checkpoint. Nếu yêu cầu mới đổi phạm vi, xác nhận theo chỉ dẫn người dùng lúc đó.
3. Giữ trạng thái unavailable/pending rõ ràng khi worker chưa có; lỗi mạng không đồng nghĩa tài khoản bị khóa. Lỗi cần lý do/mã/thời điểm và đúng account/job.
4. Không in cookie, password DB hoặc API key ra log/chat. Connection string hiện được track trong appsettings theo quyết định chủ dự án; không sao chép nó vào tài liệu. `.env`, `work/` và backup bị ignore.
5. Giữ volume Data Protection `sammicrawl_keys` để giải mã cookie cũ, volume media và legacy DB; không chạy `docker compose down -v` để sửa UI.
6. Không public trực tiếp Ollama 11434. Bridge có auth; hostname Cloudflare mong muốn `ollama.khanhdc.id.vn` chưa được cấu hình/xác minh trong repo. Không nói tunnel hoạt động chỉ vì có domain.
7. Sửa UI thì xem trang thực tế, kiểm tra trạng thái chọn/disabled/focus, biểu mẫu và màn hình hẹp. Dùng CSS/token đang có, không thay framework chỉ để làm đẹp.
8. Sau thay đổi, ghi rõ đã sửa gì, kiểm tra gì, hạn chế còn lại. Cập nhật tài liệu này nếu kiến trúc/luồng/cách chạy thay đổi.

## AI từ máy khác

Đọc REMOTE-AI.md. AiBridgeController chỉ cho Bearer RemoteAiKey, port 8081 (host 5081) chặn mọi đường dẫn ngoài bridge. AiRunner tự dùng gateway khi RemoteAi:BaseUrl có giá trị; máy chủ AI phải để trống để tránh gọi vòng. Gateway và workflow local chung singleton gate; busy trả 409, không có hàng đợi chờ AI. Không commit .env hoặc config/*.local.env.

Rewrite Facebook: AiRunner hướng dẫn văn bản thuần và FacebookPlainText.Clean loại Markdown trước khi trả/lưu, cả gateway và local. DataVaultView hiển thị phần trăm ước tính tối đa 90% khi chờ; chỉ 100% khi CreateDraft trả thành công. Chưa có tiến độ token thực từ Ollama. Không âm thầm sửa draft cũ.

RewriteOutput.Validate chặn bản trả lại nguyên văn (kể cả chỉ đổi dấu câu, hoa/thường hoặc Markdown), trả 422 thay vì lưu draft. Lời yêu cầu biên tập phải có trong user message, không chỉ system; hồ sơ thương hiệu không được lấn át chủ đề nguồn. Nguồn quá ngắn ưu tiên không bịa thêm để kéo đủ số từ; kiểm tra độ dài vẫn thực hiện trước khi duyệt.

BrandPolicy.Instructions cho rewrite chỉ truyền thông tin biên tập (giọng, đối tượng, cấu trúc, CTA, nội dung cấm/quy tắc); không truyền nguyên danh mục chủ đề/dịch vụ và thông tin hạ tầng kênh để model không tự chèn quảng cáo hoặc đổi chủ đề nguồn.

Rewrite có ảnh: ContentService đọc tối đa 4 file JPG/PNG/WebP local, 5 MB/ảnh, gửi RewriteRequest.Images cho Bonsai vision trong cùng request; không cần nạp thêm model. Gateway cũng nhận/kiểm tra Images. Thiếu file/media hoặc có video chưa phân tích trả 409, không âm thầm bỏ ảnh. Draft lưu rewriteImagesProvided và rewriteVisionModel để UI phân biệt draft cũ chưa gửi ảnh. Số ảnh gửi không phải bằng chứng OCR/diễn giải đúng; người duyệt phải đối chiếu.

29/10/2026 — Rewrite có ảnh: người dùng yêu cầu ảnh chỉ dùng để hiểu ngữ cảnh, tránh viết lạc đề. Bấm Rewrite gửi caption gốc + bytes ảnh trong cùng request Bonsai; đầu ra chỉ là phần chữ viết lại, không OCR/mô tả vật thể/bố cục và không yêu cầu sửa/xác nhận ghi chú. RewritePrompts.ImageContext được gửi trong Instructions để gateway hiện có cũng nhận quy tắc này; AiRunner local nhắc cả system/user. Giữ giới hạn/kiểm tra media, một model, chống trả nguyên văn và checklist duyệt. Draft mới lưu rewriteInputMode=caption-with-image-context, rewriteImagesProvided/rewriteVisionModel; không ghi nhận đã xác nhận ảnh. Draft cũ giữ lịch sử ghi chú/xác nhận. Endpoint visual-context/read-image còn để tương thích nhưng UI Rewrite không gọi.

Giọng rewrite: ưu tiên tone/audience/writingStyle/CTA/name riêng của kênh rồi fallback brand. Giữ đồng thời dữ kiện đúng và cách viết riêng; ẩn dụ là lối nói, không phải dữ kiện mới. Chỉ kênh có tone tech mới gợi ẩn dụ lập trình. Yêu cầu hook, đoạn ngắn, emoji phù hợp, lựa chọn đúng ảnh, CTA và hashtag; không đưa ghi chú OCR/xác nhận vào caption. Không khôi phục Markdown.

UI responsive: <=600px bảng bài/tác vụ/kênh/tài khoản hiển thị thẻ có nhãn; desktop giữ bảng. useDialogKeyboard.ts dùng chung cho modal, giữ Tab trong modal, inert nền, Escape trừ busy và trả focus về nút mở. Giữ cơ chế này khi sửa modal. Mẫu cấu hình chỉ trình bày tham số cào worker dùng; lựa chọn Repost/Rewrite nằm sau bước thu thập.

## Bảo mật — cập nhật 25/10/2026

Đọc `docs/research/SECURITY-HARDENING.md` và `tests/SecurityChecks/README.md` trước khi sửa xác thực, middleware hoặc tải media.

- `Infrastructure/SecurityServiceExtensions.cs`: đăng ký cookie, antiforgery và rate limit. Ticket mới hết hạn sau 8 giờ, không gia hạn trượt; không tự thu hồi ticket cũ. Login/setup giới hạn 10 yêu cầu/phút/IP và body 16 KB.
- `Infrastructure/RequestSecurityExtensions.cs`: header bảo mật và kiểm tra CSRF cho API thay đổi dữ liệu. `GET /api/auth/csrf` cấp token; frontend gửi `X-CSRF-TOKEN` cùng cookie. `frontend/src/api.ts` giữ token trong bộ nhớ, reset sau đổi danh tính và không tự retry mutation.
- `Infrastructure/RequestPipelineExtensions.cs` và `PublicAssetsFileProvider.cs`: giữ ranh giới port gateway, yêu cầu phiên cookie hợp lệ cho `/media`, chặn media qua static provider công khai và trả 404 cho media không tồn tại. Không bỏ bảo vệ để lách lỗi 401/403.
- `Infrastructure/FacebookMediaDownload.cs`: tải theo luồng, giới hạn 50 MB/file và 30 giây, kiểm tra HTTPS/domain/MIME, không tự theo redirect, xóa file tạm khi lỗi. `FacebookCrawlWorker` chỉ gửi cookie phù hợp URL; không log giá trị cookie. Allowlist không thay thế DNS/IP pinning hoặc kiểm tra codec.
- Bridge Bearer và worker key không được cấp quyền xem media hoặc gọi API ngoài phạm vi. Giữ riêng các cơ chế xác thực này.

Chạy bộ kiểm tra từ thư mục gốc:

```powershell
docker build -f tests/SecurityChecks/Dockerfile -t sammicrawl-security-checks .
docker run --rm sammicrawl-security-checks
```

Mốc kiểm tra hiện tại: 43 assertion đạt, frontend và image app build thành công. Fixture dùng controller/middleware thật với dịch vụ giả, không dùng live DB để dựng dữ liệu test. Đây không phải pentest toàn diện hoặc bằng chứng Facebook chạy thật.

Không tự xóa chức năng xem cookie mà người dùng yêu cầu; không tự đổi connection string/demo credential hoặc viết lại lịch sử Git. Secret đang được track, demo công khai, thu hồi phiên, TLS/trusted proxy và giới hạn media còn lại được ghi trong báo cáo. Cần xử lý các điểm này trước khi mở UI ra Internet; không coi build/test pass là đã an toàn để public.

## Đồng bộ nhiều người dùng — 28/10/2026

- SignalR `/api/live` dùng cookie đăng nhập dự án; negotiate/transport POST vẫn qua CSRF. Hub chỉ gửi sự kiện `DataChanged`, không gửi dữ liệu tài khoản/cookie/nội dung. Gateway port 5081 không cho truy cập hub. Đóng kết nối khi ticket hết hạn; kiểm tra Origin khi mở kết nối.
- `Services/LiveUpdates.cs` gộp thông báo trong 250 ms. `ProjectRepository` báo thay đổi sau SaveChanges thành công hoặc commit transaction ingest/cập nhật brand thành công. Không phát trước commit.
- `frontend/src/liveUpdates.tsx`: kết nối khi đã đăng nhập, tự nối lại và tải lại danh sách khi nối lại/focus cửa sổ. Các trang báo cáo/bài viết/kênh/thương hiệu/tài khoản/mẫu cấu hình/thùng rác/hàng đợi dùng chung revision để tải lại; bỏ polling 10 giây ở shell và thư viện bài.
- Form/modal đang mở giữ state nhập riêng; hiện cảnh báo khi danh sách được làm mới. SignalR không thay thế kiểm tra phiên bản: bảo vệ conditional update brand/kênh vẫn giữ; chưa bổ sung khóa đồng thời cho các loại khác.
- Phạm vi một process backend, shared project như quyền API hiện có. SQL trực tiếp và thay đổi từ replica khác không tự phát sự kiện; cần tải lại thủ công/focus. Nhiều replica cần backplane và cơ chế báo thay đổi sau commit dùng chung; chưa triển khai trong bản này.
- Kiểm tra SignalR nằm cùng `tests/SecurityChecks/`: fixture không DB/FB/Ollama, kiểm tra CSRF/auth/port gateway và hai WebSocket client cùng nhận invalidation.

Giới hạn tác vụ cào: mỗi tài khoản chỉ được dùng một lượt; tổng maxPosts phải <= min(1000, số tài khoản cào đủ điều kiện × accountPostLimit). Form hiển thị sức chứa và chặn chạy nếu vượt; API kiểm tra lại danh sách tài khoản thực trước khi tạo task, trả 400 CRAWL_CAPACITY_EXCEEDED. Không tự hạ thông số hoặc quay vòng tài khoản. Mẫu cấu hình có thể lưu để tái sử dụng, nhưng luôn kiểm tra lại sức chứa khi chạy; trần này không bảo đảm lấy đủ bài nếu nguồn ít, trùng bài hoặc lỗi phiên.

## Cào và đánh giá Clef — 28/10/2026

Worker đánh giá Clef tuần tự ngay sau mỗi bài mới được lưu, trước khi xử lý bài tiếp theo. StartCrawl kiểm tra AI online/có clef-flash:latest/không busy trước khi tạo task (không suy luận và không chứng minh endpoint systemone hoạt động). Giữ singleton gate, không chạy song song model. Nếu đánh giá lỗi/busy/kết quả không hợp lệ, dừng lượt ở paused/CLEF_EVALUATION_INCOMPLETE, giữ bài pending để đánh giá thủ công, không tự retry Facebook hoặc đổi model.

Task chỉ completed/100% khi đủ số bài thu thập và tất cả có kết quả Clef hợp lệ; running tối đa99%, đủ bài chưa đánh giá là90%. maxPosts và accountPostLimit vẫn tính bài mới thu thập, kể cả bài sau đó bị loại; không tự cào bù số bài không phù hợp. Task lưu clefEvaluatedPosts/clefRecommendedPosts/clefReviewPosts/clefRejectedPosts. Queue hiển thị thống kê và lý do dừng.

ContentService đánh giá rejected thì soft-delete bài với deletedReason=clef_rejected: không còn trong danh sách/báo cáo, không tạo draft; vẫn giữ record/media/kết quả để chống trùng và đối chiếu, không xóa file vật lý. recommended giữ điểm/lý do/chủ đề; review (nhất là media chưa phân tích được) giữ riêng để người duyệt, không tự xem là phù hợp. Draft cũ giữ snapshot, không xóa theo bài nguồn. Không tự áp dụng lại đánh giá cho bài lịch sử.

Khi khởi động, các task Facebook local cũ completed nhưng còn bài gắn crawlTaskId chưa đánh giá được chuyển paused/CLEF_EVALUATION_INCOMPLETE; không replay. Không suy ra task thiếu mọi bài nguồn (đã bị xóa) là lỗi. Khi đánh giá lại thủ công, thống kê task cập nhật; chỉ tự hoàn tất task tạm dừng vì Clef khi đủ bài và đủ kết quả, không tiếp tục cào. Các tác vụ tạm dừng do người dùng không tự resume.

Danh sách Bài viết / media phân trang phía frontend sau bộ lọc, mặc định 10 bài/trang, chọn 10/20/50; đổi bộ lọc hoặc cỡ trang về trang đầu, SignalR cập nhật giữ trang nếu còn hợp lệ. API records/articles vẫn trả toàn bộ danh sách; chưa có phân trang truy vấn DB.

Đọc ảnh/rewrite kiểm tra AI online và có bonsai-2:27b trước khi unload/gửi ảnh/suy luận (cả local/gateway). Thiếu model hoặc mất kết nối trả503 AI_MODEL_UNAVAILABLE với lý do cụ thể ở ContentService; không fallback9b và không tự xác nhận ghi chú. Đây là kiểm tra availability, không bảo đảm model vision suy luận đúng.

Clef đánh giá thủ công: DataVaultView hiển thị tiến độ ước tính và thời gian chờ tại dòng bài, vùng trạng thái và modal. Tăng tối đa90% khi chờ, chỉ100% khi API thành công; lỗi dừng thanh tiến độ và hiện lý do, không tự retry. Đây không phải tiến độ token thật. Kết quả rejected loại bài khỏi state danh sách ngay; modal giữ kết quả và không cho tạo draft từ bài đã loại.

29/10/2026: Modal bài viết cho phép đóng/Escape khi chờ AI (rewrite/Clef); chỉ mutation lưu/duyệt vẫn khóa đóng khi busy. Đóng UI không hủy yêu cầu đã gửi, không tự retry; chặn mở bài khác khi busy. Rewrite gồm hiểu ảnh và viết lại caption trong một request, tiến độ ước tính tối đa90%, chỉ100% khi tạo draft thành công. Modal min-height0/flex/overflowauto, vùng tiến độ riêng giới hạn chiều cao; giữ cuộn/đóng được khi chờ AI.

Sau POST /api/crawl/start thành công, CrawlerStudioView gọi onViewQueue để chuyển sang Hàng đợi tác vụ và shell tải lại danh sách task. Request lỗi giữ nguyên form và hiển thị lý do; không tự retry. Nút Xem danh sách bài vẫn mở thư viện theo thương hiệu.

Hàng đợi hiển thị thống kê Clef gọn ở cột tiến độ; lý do/mã lỗi đầy đủ chỉ nằm trong details Xem chi tiết tại cột Trạng thái, không lặp dưới Mục tiêu và Tiến độ. Tác vụ Clef chưa xong có nhãn ngắn và số bài chờ.

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

## Metadata tương tác bài nguồn — 29/10/2026

FacebookCrawlWorker đọc bộ đếm hiển thị trong phạm vi bài bằng FacebookPostMetadata.Script, không bấm cảm xúc/bình luận, không gọi API Facebook riêng. Lưu reactions (tổng cảm xúc), likes chỉ khi có nhãn Thích rõ ràng, comments, raw CountText và CountApproximate cho số rút gọn, interactionCapturedAt UTC. Thiếu/ẩn không coi là0; loại số trong caption, comment lồng, phần tử ẩn và container lẫn nhiều bài. Chưa hỗ trợ bộ lọc tương tác, không tải nội dung bình luận. DOM khác chưa nhận diện có thể để trống.

Giờ nguồn lưu sourcePublishedAt UTC chỉ từ data-utime hoặc ISO datetime có múi giờ hợp lệ; thiếu thì giữ sourcePublishedText, không đoán giờ tuyệt đối từ nhãn tương đối. createdAt vẫn là thời gian lưu/thu thập. UI phân biệt Thu thập / Đăng gốc, raw nhãn hiển thị Facebook lúc cào; số K/M/nghìn/triệu có dấu≈. Bài đã lưu trước thay đổi không tự backfill và bài trùng không bị sửa metadata; áp dụng cho bài mới thu thập.

Kiểm tra độc lập tests/FacebookMetadataChecks dùng Chromium thật với HTML fixture và code mapper production, chặn toàn bộ mạng; không đọc DB/phiên Facebook/Ollama. Chưa chạy thử Facebook thật trong lượt bổ sung; fixture pass không bảo đảm mọi layout Facebook đều đọc được.
