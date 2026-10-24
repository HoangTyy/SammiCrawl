# Nghiên cứu cải tiến SammiCrawl — sổ làm việc

**Trạng thái mới nhất: hoàn tất vòng nghiên cứu/cải tiến giao diện local ngày 24/10/2026; xem phần kết quả cuối bên dưới và SOURCES.md. Các mục đang làm ở đầu tài liệu là nhật ký các lượt trước.**

Ngày kiểm tra: 24/10/2026 (Việt Nam). Nhiệm vụ đang thực hiện, chưa phải kết luận cuối. Không push/commit, không đăng Facebook. Bản đối chiếu dưới đây do agent điều phối lập từ mã nguồn và kiểm tra giao diện; không gán là kết luận của Hermes khi chưa có phản hồi cuối.

## Trạng thái Hermes

- Docker web tools gặp DNS error; chuyển sang Hermes Windows, profile nghiên cứu riêng.
- Profile runtime 96K; chỉ model bonsai-2-hermes:27b. Không thay cấu hình Hermes chính.
- Phiên 20261024_210002_4df14c đã gọi web_search/web_extract thành công; raw evidence ở work/deep-research/hermes-research-round-1.jsonl.
- Ba URL chính thức đã được Hermes trích xuất: Buffer drafts, Sprout publishing và W3C form grouping. Sprout extract chứa nhiều navigation nên chưa coi là đã đọc sâu nội dung tính năng. Các kết quả search blog/nguồn không liên quan không dùng làm căn cứ triển khai.
- Cần vòng tiếp theo tập trung thông số, vòng đời task và đề xuất cụ thể từng màn hình; không lặp vô hạn việc tìm kiếm cùng một nguồn.

## Nguồn đã kiểm tra độc lập

1. https://support.buffer.com/articles/managing-and-approving-draft-posts-57li7M8tDA — drafts và bước yêu cầu/duyệt có trạng thái khác nhau; quyền khác nhau cho thao tác bản nháp. Chỉ tham khảo pattern, chưa triển khai RBAC.
2. https://support.sproutsocial.com/hc/en-us/articles/360000576466-Introduction-to-Publishing — nội dung nháp, lịch và phê duyệt nằm trong khu Publishing; áp dụng nguyên tắc gom tác vụ nội dung, không sao chép toàn bộ chức năng sản phẩm lớn.
3. https://www.w3.org/WAI/tutorials/forms/grouping/ — nhóm trường liên quan bằng fieldset/legend và label rõ để dễ hiểu biểu mẫu.
4. https://docs.apify.com/actors/running/runs-and-builds — một run có input/output/log và trạng thái riêng; phù hợp tách theo dõi run khỏi nơi soạn cấu hình.
5. https://docs.apify.com/actors/development/actor-definition/input-schema/specification/v1 — input schema có bounds/default/prefill và đơn vị; nguồn cần Hermes đọc sâu thêm.

## Đối chiếu đủ 9 màn hình

| Màn hình | Hiện có | Đề xuất cần Hermes phản biện |
|---|---|---|
| Báo cáo tổng quan | Báo cáo nội dung, lịch dự kiến, launcher cũ, nhật ký | Tách báo cáo và nhật ký; bỏ launcher cũ dùng từ Cluster. Giữ phân biệt lịch dự kiến/chưa có worker đăng. |
| Cào bài | Mẫu đã lưu, brand/account, URL, số bài/cuộn, khoảng nghỉ, bộ lọc tương tác | Nhóm trường cơ bản/nâng cao. UI không cho bật các tham số backend chưa hỗ trợ. Hiển thị đơn vị giây nhưng giữ API ms; min/max phải đồng bộ. |
| Hàng đợi | Pipeline đồ họa dự kiến và bảng run | Ưu tiên run thật: tiến độ, bài trùng, mã lỗi, thời gian. Thêm lọc Tạm dừng, bỏ latency luôn 0 và Mission/Stealth. |
| Bài viết & duyệt đăng | Bài nguồn, Clef, draft, review ảnh, rewrite, checklist | Giữ hành động trong ngữ cảnh bài. Thêm lọc bản nháp theo trạng thái/kênh; cảnh báo thiếu từ, media và giờ dự kiến. Không tạo AI Rewriter riêng. |
| Kênh đăng | Group/Page theo brand, account phụ trách, quy chuẩn | Giữ dữ liệu kênh trong brand. Tên/loại/trạng thái rõ; trạng thái account không đồng nghĩa quyền đăng. |
| Tài khoản Facebook | Vai trò cào/đăng, bật/tắt, health, cookie, kiểm tra | Giữ vai trò tách. Phân biệt bật/tắt với hết phiên/khóa; hướng xử lý lỗi kế bên trạng thái. Không đổi cookie tự động. |
| Thương hiệu | Danh sách và hồ sơ nhập từ workbook | Giữ brand là hồ sơ giọng/độc giả/quy tắc; liên kết kênh có ngữ cảnh. Không đưa lại KPI tổngbrand/kênh đã bỏ. |
| Mẫu cấu hình cào | Lưu/sửa/áp dụng preset | Nêu rõ lưu mẫu không chạy; áp dụng phải xử lý giá trị cũ chưa được hỗ trợ và brand đã ngưng. |
| Thùng rác | Ba nhóm brand/preset/account và khôi phục | Giữ phân nhóm rõ, chỉ khôi phục, không thêm xóa vĩnh viễn. |

## Sổ tham số hiện hành (từ mã nguồn, không phải khuyến nghị của Facebook)

| Biến API | Default UI | Bounds backend | Ghi chú |
|---|---:|---|---|
| maxPosts | 300 | 1–1000 | Số bài mới cần lưu, không đếm bài trùng |
| maxScrolls | 100 | 1–2000 | Giới hạn cuộn có thể hết nguồn sớm |
| delayMinMs / delayMaxMs | 1500 / 4000 | 500–120000 | Nghỉ giữa bài; min <= max |
| accountPostLimit | 100 | 1–1000 | Giới hạn bài mỗi account |
| accountRestMinMs / accountRestMaxMs | 30000 / 60000 | 1000–3600000 | Nghỉ giữa account; min <= max |
| minLikes / minComments | 0 / 0 | Hiện chỉ hỗ trợ 0 | Backend từ chối >0; UI hiện còn cho nhập nên cần chỉnh |
| includeComments | false | Hiện chỉ hỗ trợ false | Không trình bày là chức năng đã hoạt động |
| images | tối đa 4 | 5 MB/ảnh, JPEG/PNG/WebP | Thiếu media hoặc video chưa phân tích cần xem thủ công |
| visualContext | người thao tác xác nhận | 1–8000 ký tự; không [không rõ] | Fingerprint đối chiếu ảnh+caption trước rewrite |
| rewrite progress | ước tính | 0–90 lúc chờ, 100 khi lưu xong | Không phải % token thực |

Khoảng nghỉ không bảo đảm tránh giới hạn Facebook. Không thêm cơ chế vượt hạn chế hay tự đăng trong nghiên cứu UI.

## Vòng 2 và triển khai đầu tiên — 24/10/2026 21:10 VN

Hermes tiếp tục cùng session `20261024_210002_4df14c`, hoàn tất exit 0. Kết quả thô nằm trong `work/deep-research/hermes-round-2-result.txt` (ignored). Đã gọi web_extract cho Apify runs/input schema, W3C disclosure navigation và Buffer pre-scheduled drafts. Một số trích xuất bị rút gọn; không xem toàn bộ kết luận model là bằng chứng nguồn.

### Quyết định kiểm duyệt

- Chấp nhận nguyên tắc tách lượt cào và bản nháp; lịch dự kiến không đồng nghĩa đăng tự động.
- Không sao chép trạng thái RUNNING/SUCCEEDED/ABORTED/TIMED-OUT của Apify vào SammiCrawl. Backend hiện dùng pending/running/paused/completed/failed.
- Không thêm Abort/Tạm dừng/Thử lại khi chưa có API tương ứng.
- Không dùng phép so sánh Sprout approval để suy ra sức khỏe hay quyền đăng Facebook.
- Không tạo giờ đăng cố định ở cấp kênh chỉ vì Buffer hỗ trợ giờ của từng draft.
- Hermes nói thiếu kiểm tra min <= max là sai: CrawlerStudioView đã có validate. Giữ nguyên validation.
- Giữ phần trăm rewrite có nhãn ước tính theo yêu cầu người dùng; không bỏ chỉ vì chưa có token streaming.
- Chưa chấp nhận nguyên trạng nhóm menu Hermes: Kênh đăng thuộc vận hành nội dung, không phải nguồn cào; không nhân đôi mục duyệt đăng trong Hàng đợi.

### Đã áp dụng local

1. Hàng đợi: bỏ sơ đồ topology dự kiến/Stealth/comments không phản ánh worker hiện tại; thêm Tạm dừng và số lượng theo từng trạng thái; thay độ trễ 0ms bằng số bài trùng bị bỏ qua; hiện mã lỗi cùng lý do; tên tìm kiếm có accessible label; phân biệt rỗng thật và không khớp bộ lọc.
2. Cào bài: bỏ trường nhập likes/comments và checkbox lấy bình luận chưa hỗ trợ. Mẫu cũ vẫn giữ nguyên giá trị, hiện cảnh báo và nút chủ động bỏ các tùy chọn đó; chặn chạy/lưu khi chưa bỏ. Không âm thầm sửa mẫu DB.

### Kiểm tra

- npm run build: đạt. docker compose build app: đạt. git diff --check: đạt.
- Triển khai container app local; trước restart xác minh chỉ có 1 tác vụ paused và 1 completed, không có tác vụ đang chạy.
- UI thật: bộ lọc Tạm dừng (1) chỉ còn đúng tác vụ LOGIN_REQUIRED; cột bỏ qua trùng và mã lỗi xuất hiện. Form cào không còn điều khiển chức năng chưa hỗ trợ.
- Chưa tạo/cào/đăng/sửa dữ liệu DB trong kiểm tra.
- Ollama sau vòng 2: một model bonsai-2-hermes:27b, context 98304, size == size_vram 15381540370 bytes. Không ghi nhận offload model sang RAM từ API ps. Đây không phải cam kết tổng RAM hệ thống bằng 0.

### Còn phải làm

- Chốt và áp dụng menu hai tầng, rà soát đủ chín màn hình; sửa tên Mission/Cluster còn sót.
- Lọc bản nháp theo kênh/trạng thái thực; nhóm form và thông báo media; kiểm tra responsive/dark/focus.
- Bộ đếm filter đang sát chữ; cần CSS badge/gap. Bảng hàng đợi có thanh cuộn ngang trên cửa sổ hẹp, cần thu gọn cột ưu tiên trạng thái.
- Chưa hoàn tất toàn bộ nghiên cứu/cải tiến. Giữ lịch 10 phút, không commit/push.

## Lượt áp dụng menu và bộ lọc — 24/10/2026 21:20 VN

Không có tiến trình nghiên cứu Hermes còn chạy; không khởi tạo thêm model. Dùng các phát hiện đã đối chiếu ở hai vòng trước, không coi suy luận giao diện của Hermes là đặc tả backend.

### Đã sửa

- Menu hai tầng 5 nhóm: Tổng quan (báo cáo), Thu thập (cào/hàng đợi/mẫu), Nội dung (bài/duyệt + kênh), Quản lý (thương hiệu + Facebook), Thùng rác (các danh mục khôi phục cũ). Giữ tất cả 9 NavigationTab; không thêm AI Rewriter, không nhân đôi queue duyệt và queue cào.
- Nhóm hiện tại bấm lại không reset trang con; aria-current ở trang đang chọn, focus-visible và responsive cho menu mới.
- Bản nháp: lọc theo trạng thái backend thực và kênh snapshot; ID bộ lọc ghép brand + channel tránh lẫn ID legacy giữa thương hiệu. Đổi thương hiệu reset kênh lọc. Hiện số kết quả/tổng theo brand.
- Ghi chú ảnh: hiển thị số ký tự/8000 và giới hạn ảnh đang được backend hỗ trợ.
- Hàng đợi: badge số lượng có khoảng cách, bỏ nhãn Mission; bảng thu gọn và cuộn ngang có kiểm soát.
- Tạo nhanh ở báo cáo: đổi nhãn thành Điền cấu hình, mô tả rõ chỉ mở form và chưa chạy; bỏ thông điệp Cluster tự động sai hiện trạng.
- Đã phát hiện CSS tên workspace-navigation có sẵn trong WorkspaceView; menu mới dùng app-navigation riêng, tránh ghi đè bố cục trang thương hiệu/thùng rác.

### Xác minh đã có

- Frontend TypeScript/Vite build và Docker build đạt; triển khai local, git diff --check đạt.
- UI thật bộ lọc approved_pending_worker trả 0/10, awaiting_review + kênh StuX trả 10/10. Không lưu/sửa/duyệt bản nháp.
- Kiểm tra trang Kênh đăng và Thương hiệu truy cập được từ menu mới. Đang tiếp tục kiểm tra đầy đủ 9 trang, dark/focus và cửa sổ hẹp; chưa kết luận toàn bộ hoàn tất.

### Bổ sung kiểm tra giao diện thực tế

- Menu mới đã hiển thị đủ chiều rộng cửa sổ ~850px, nhãn không còn xuống dòng; chọn nhóm Thu thập mở Cào bài, mở được Hàng đợi và Mẫu cấu hình. Mẫu cấu hình trống hiển thị hướng dẫn lưu từ form.
- Thương hiệu: đã tải đủ 8 hồ sơ, grid 2 cột; thanh tìm kiếm/ngôn ngữ không bị CSS menu mới ảnh hưởng.
- Tài khoản: đã tải 2 bản ghi và nhãn Hết phiên/Chưa xác minh đúng dữ liệu. Không mở chỉnh cookie, không bấm kiểm tra Facebook.
- Thùng rác: chuyển được các danh mục thương hiệu/mẫu/tài khoản; giao diện tối đọc được và nút hiện trạng thái rõ. Không khôi phục/xóa dữ liệu; đã trả theme về sáng.
- Hàng đợi: bộ đếm đã có badge và khoảng cách. Bảng còn cuộn ngang ở cửa sổ hẹp; lượt kế tiếp cần ưu tiên cột trạng thái và rà focus/responsive hẹp hơn.
- Chưa xác minh viewport điện thoại thật và mọi modal; không ghi kiểm tra hoàn tất cho các phần đó.

## Responsive và modal — lượt 21:30 VN

Nguồn bổ sung được Codex trực tiếp mở đọc: https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/ . Áp dụng nguyên tắc chuyển focus vào modal, giới hạn Tab/Shift+Tab, Escape và khôi phục focus; không suy ra chức năng nghiệp vụ mới từ hướng dẫn accessibility.

- Đã thử viewport 390x844 bằng khả năng viewport của trình duyệt. Trước sửa: thanh điều khiển bị xuống dòng, bảng giấu trạng thái/thao tác phía ngoài màn hình.
- CSS <=600px chuyển bảng tác vụ, bài/bản nháp, kênh và tài khoản sang thẻ có nhãn trường. Trạng thái tác vụ lên đầu thẻ. Desktop giữ bảng. Báo cáo số liệu giữ bảng so sánh/cuộn ngang.
- Thu gọn toolbar mobile; menu con cuộn ngang một hàng, giữ toàn bộ công cụ. Media trong modal một cột.
- useDialogKeyboard dùng chung ở Workspace, Channels, FacebookAccounts và DataVault: giới hạn focus, inert nền, khóa scroll nền, khôi phục khi đóng; Escape bị chặn lúc busy để không bỏ ngang request.
- Không mở cookie đã lưu để kiểm tra, không thay dữ liệu DB. Đang kiểm tra bản triển khai mới trên UI thật; chưa kết luận hoàn tất.

### Kết quả QA bổ sung

- Modal bài nguồn ở 390 px: focus ban đầu ở Đóng; Shift+Tab đến nút cuối, Tab từ nút cuối quay lại Đóng; Escape trả focus về Repost / Rewrite. Không gọi evaluate/rewrite/repost. Sau đóng, không còn phần tử inert.
- Modal thêm tài khoản trống: Escape trả focus về Thêm tài khoản, không lấy cookie đã lưu. Modal thương hiệu và thêm kênh mở đủ trường trên mobile; không nhập/lưu dữ liệu.
- Đã sửa hàng nút modal gây cuộn ngang, chuyển form hai cột về một cột ở <=600px, badge lọc tác vụ 2 cột không chồng chữ. Nút icon thao tác mobile tối thiểu 36px.
- Thẻ kênh trước đó bị width32% của bảng desktop ghi đè; đã sửa selector. Tooltip ẩn cũng gây tràn ngang dù không nhìn thấy; mobile chuyển tooltip xuống đáy viewport, thead ẩn có containment.
- Mẫu cấu hình: bỏ UI nhánh rewrite, approval, tham số AI không được crawler dùng; giữ các giá trị legacy trong bản ghi. Bổ sung các trường accountPostLimit/accountRestMinMs/accountRestMaxMs được worker dùng. Không thay bounds backend.
- Hermes vòng 3 tiếp tục session cũ, chỉ một lượt extract W3C notifications và Apify monitoring; đang chờ kết luận. Raw log: work/deep-research/hermes-research-round-3.jsonl. GPU API ps cho thấy duy nhất bonsai-2-hermes:27b, context98304 và size==size_vram.

## Vòng phản biện cuối và phạm vi chấp nhận

Hermes vòng 3 hoàn tất exit0 sau khoảng266 giây, cùng session `20261024_210002_4df14c`. Trả lời lưu tại `work/deep-research/hermes-round-3-result.txt`. Có một batch web_extract W3C User Notification và Apify Monitoring. Đây là nghiên cứu của model, không phải kết quả kiểm thử.

Các điểm phải bác bỏ trong kết quả thô:
- Checklist mang tên read-only nhưng đề nghị chạy crawler 300–1000 bài sẽ tạo dữ liệu; KHÔNG thực hiện.
- Không cần endpoint pause riêng chỉ để lọc status paused đã được API records/tasks trả về. Đã kiểm tra UI có1 paused/1 completed.
- Không có bộ lọc ngày xóa ở Thùng rác hay trạng thái rejected của draft; model nêu như hiện có là sai. Không thêm để hợp thức hóa lời model.
- Không dùng các trạng thái READY/SUCCEEDED của Apify thay trạng thái SammiCrawl; không coi công cụ khác là đặc tả app.
- Không có endpoint normalize preset mới; UI dùng nút chủ động bỏ3 tùy chọn chưa hỗ trợ rồi server vẫn validate. Không tự thay DB.
- Tài liệu disclosure không chứng minh focus-trap modal. Nguồn phù hợp là W3C dialog-modal đã được Codex đọc trực tiếp và kiểm tra bàn phím.
- Triển khai local không đòi hỏi push GitHub; đã build và chạy local, chưa commit/push.

### Kết quả cuối của vòng cải tiến này

- Đã đối chiếu cả9 màn hình; giữ mỗi công cụ một mục đích, không thêm workflow wizard hay AI Rewriter riêng.
- Menu hai tầng, trạng thái hàng đợi thực, lọc bản nháp, preset chỉ có tham số crawler dùng, UI mobile và modal keyboard đã triển khai local.
- TypeScript/Vite và Docker build đạt; git diff --check đạt. Mobile390px: Kênh và Facebook có scrollWidth bằng clientWidth385px (trừ scrollbar); modal bài không còn phần tử con tràn ngang.
- Kiểm tra bàn phím, dark/light, form trống thương hiệu/kênh/tài khoản; không lưu dữ liệu. Desktop1280px vẫn giữ bảng và đủ thao tác.
- Không thử CRUD ghi DB, không crawl/rewrite/approve/publish trong QA; busy của request đang chạy và screen reader/điện thoại vật lý chưa được thử. Không tuyên bố các phần đó đã pass.
- Worker đăng chưa triển khai, lịch vẫn dự kiến. Session Facebook hiện có vấn đề không được sửa hay tự retry trong tác vụ giao diện.
- Không có rủi ro P0 mới được xác nhận còn chặn vòng giao diện này; các khả năng ngoài phạm vi không tự bổ sung.

Tài liệu này và PROJECT-RESEARCH-MEMORY.md là bộ nhớ nghiên cứu cấp dự án. Raw log/ảnh kiểm tra ở work/deep-research bị gitignore. Hoàn tất vòng nghiên cứu và cải tiến giao diện đã cho phép; dừng lịch theo dõi sau kiểm tra bản build cuối, không push.

Lưu ý kiểm chứng nguồn cuối: một số tool_result Hermes bị logger cắt ở ~5000 ký tự; không khẳng định đã đọc đủ mọi URL trong batch. SOURCES.md phân biệt4 result JSON đầy đủ, đoạn trích bị cắt, URL mới chỉ được yêu cầu, và phần Codex trực tiếp mở để bù kiểm chứng. Kết luận model không thay thế bằng chứng source/runtime.

Chốt bàn giao: build cuối đã triển khai local; console trang không có lỗi JavaScript trong lượt kiểm tra cuối. Đã reset viewport về kích thước trình duyệt ban đầu, trả theme sáng, đóng mọi modal. Ollama không còn model được nạp sau khi kết thúc Hermes. Lịch ki-m-tra-nghi-n-c-u-sammicrawl-m-i-10-ph-t đã xóa thành công. Không commit/push, không đăng Facebook, không sửa dữ liệu người dùng trong QA.
