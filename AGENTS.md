# SammiCrawl — hướng dẫn cho AI và người tiếp quản

Cập nhật: 23/10/2026. Đọc file này trước khi sửa mã; sau đó đọc `LOCAL-SETUP.md` và các file liên quan đến tác vụ. Đây là mô tả hiện trạng, không phải bằng chứng rằng worker đã hoạt động. Kiểm tra lại source/runtime khi tiếp quản.

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

Báo cáo tổng quan · Cào bài · Hàng đợi tác vụ · Bài viết & duyệt đăng · Kênh đăng · Tài khoản Facebook · Thương hiệu · Mẫu cấu hình cào bài · Thùng rác.

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

Không có bộ test tích hợp độc lập đã đóng gói trong repo; script thử ở `work/` bị ignore và không có sẵn khi pull. Dùng fixture riêng khi cần, không sửa/xóa dữ liệu thật để thử. UI pass/build pass không chứng minh Facebook đã cào/đăng thật.

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
