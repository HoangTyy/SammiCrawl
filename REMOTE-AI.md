# Dùng AI trên máy chủ SammiCrawl

Máy chủ chạy Ollama và SammiCrawl. Máy client chỉ cần backend SammiCrawl; không cần tải model. Cả Clef và Bonsai dùng API gateway, chung khóa một yêu cầu/model tại một thời điểm. Khi bận trả HTTP 409 với Retry-After, không tự xếp hàng. Không bảo đảm độc quyền với chương trình khác gọi Ollama trực tiếp.

## Máy chủ AI

- Chạy Ollama với clef-flash:latest và bonsai-2:27b.
- `.env`: REMOTE_AI_KEY là secret ngẫu nhiên ít nhất 32 ký tự. Để REMOTE_AI_BASE_URL và REMOTE_AI_API_KEY trống.
- `docker compose up -d --build app`. Nếu shell có OLLAMA_BASE_URL=http://localhost:11434, xóa biến phiên bằng Remove-Item Env:OLLAMA_BASE_URL trước khi chạy Compose; localhost trong container không phải máy Windows.
- Cloudflare Tunnel riêng tên StuXD (6f922821-6cbe-459d-8630-33d3c0cc4763): Published application route `ollama.khanhdc.id.vn`, service HTTP `host.docker.internal:5081` vì connector chạy trong Docker. Giữ tunnel Zalo/Hermes riêng. Hostname đã được cấu hình trên máy chủ và kiểm tra HTTPS ngày 24/10/2026; không tự xuất hiện khi pull repo.
- Cổng 5081 chỉ cho /bridge/*; UI, tài khoản và media không truy cập được qua cổng này. Không public Ollama 11434 hoặc cổng UI 5080.
- Máy chủ, Docker, Ollama và tunnel phải đang chạy thì client mới dùng được.

## Máy client

Chạy `./scripts/Configure-RemoteAi.ps1`, nhập secret do chủ máy AI cấp. Hoặc thêm vào `.env` (không commit):

```dotenv
REMOTE_AI_BASE_URL=https://ollama.khanhdc.id.vn
REMOTE_AI_API_KEY=<secret-do-chu-may-cap>
```

Sau đó `docker compose up -d --build app`. Khi chạy .NET trực tiếp, dùng biến môi trường RemoteAi__BaseUrl và RemoteAi__ApiKey tương ứng. Base URL là gốc domain, không thêm /bridge.

Máy chủ có thể cung cấp riêng file `config/remote-ai-client.local.env` đã tạo trên máy; file này bị Git bỏ qua. Chuyển riêng bằng kênh bảo mật rồi nhập vào .env client. Không dán secret vào GitHub, frontend hoặc log. Cấu hình này dùng shared key, chưa có phân quyền/thu hồi riêng từng account. Đăng nhập dự án trên client vẫn cần thiết để dùng API nội bộ của client.

## API

Tất cả endpoint cần header `Authorization: Bearer <secret>`, trả Cache-Control: no-store.

- GET /bridge/health: trạng thái AI và danh sách model, không trả thông tin DB.
- POST /bridge/rewrite: JSON {"text":"Nội dung gốc", "tone":"thân thiện", "instructions":"Giữ nguyên thông tin", "temperature":0.7}; kết quả {"text":"..."}.
- POST /bridge/evaluate: JSON {"text":"Nội dung", "profile":{"name":"Thương hiệu", "description":"Chủ đề"}, "images":[]}; ảnh là base64 JPEG/PNG/WebP, tối đa 4 ảnh, mỗi ảnh 5 MB. Kết quả gồm status, score, reason, topic, warnings, decision.
- 401: thiếu/sai key; 400: dữ liệu không hợp lệ; 409: AI bận; 429: quá 30 request/phút; 502: model lỗi; 503: gateway bị cấu hình trỏ sang gateway khác.

Gateway không lưu nội dung gọi trực tiếp vào DB. Các thao tác qua workflow client vẫn lưu kết quả ở DB của client. Đổi key: cập nhật REMOTE_AI_KEY trên máy chủ, recreate app, cấp key mới cho client. Key cũ hết hiệu lực sau khi recreate. Chỉ một replica gateway để khóa model có hiệu lực toàn bộ request.

## Connector StuXD trên máy chủ

Chạy `docker compose --profile ai-host up -d ai-tunnel`. Connector tự restart cùng Docker, dùng file token local `config/stuxd-tunnel-token.local.env` (không commit). Máy client không chạy profile ai-host và không cần token Cloudflare. DNS CNAME ollama trỏ tới 6f922821-6cbe-459d-8630-33d3c0cc4763.cfargotunnel.com, Proxied. Không thay service Cloudflared Windows đang phục vụ Zalo/Hermes.

Rewrite cũng nhận trường images: danh sách base64 JPEG/PNG/WebP, tối đa 4 ảnh, mỗi ảnh 5 MB. Bonsai đọc ảnh cùng caption trong một request, vẫn chỉ một model. Bản nháp có rewriteImagesProvided để biết ảnh đã được gửi; không coi đó là xác minh OCR hay tính phù hợp tự động. Video chưa có phân tích khung hình.

Bước đối chiếu ảnh: backend client gọi /bridge/rewrite với mode=read-image và images để phiên âm; UI cho người thao tác sửa/xác nhận. Lần rewrite tiếp theo nhận caption cùng ghi chú đã xác nhận, không nhận lại ảnh. Cả hai lần dùng Bonsai và chạy tuần tự qua gate. Đọc ảnh không tạo bản nháp.

## Gateway gián đoạn — 29/10/2026

Client chỉ thử lại tối đa một lần (nghỉ2giây) với POST bridge/evaluate thuần suy luận khi nhận HTTP502/504/520/522/524 chưa có mã lỗi xác định. Tổng2lần gọi, giữ chung gate; không retry409 busy/429 rate limit/auth/lỗi output Clef đã xác định, không tự retry rewrite/DB mutation hoặc Facebook. Hết lần thử vẫn lỗi thì giữ pending, task paused và không báo100%.

Gateway mới trả mã lỗi phân biệt CLEF_INVALID_OUTPUT, AI_UPSTREAM_FAILED, AI_TIMEOUT và log code/type/trace, không log payload/cookie/key. Client hiển thị mã/lý do an toàn thay vì che mọi lỗi bằng một câu chung. Cần pull và build/recreate app trên cả máy client lẫn máy chủ AI; máy AI phải giữ RemoteAi:BaseUrl trống để không gọi vòng. Giữ .env và volume khóa/media trên mỗi máy; file env thiếu dấu chấm được ignore, Compose vẫn cần .env.

Đánh giá lại hết các bài của tác vụ dừng vì Clef: nếu đã đủ mục tiêu thì completed; nếu mới thu thập4/10 thì vẫn paused/COLLECTION_INCOMPLETE, báo còn thiếu bài thu thập thay vì ghi Clef còn0bài chờ. Không tự resume Facebook.
