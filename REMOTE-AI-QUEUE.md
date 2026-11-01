# Hàng đợi AI tại gateway

Một gateway SammiCrawl phục vụ nhiều backend client; Clef/Bonsai xử lý tuần tự. Không chạy nhiều replica gateway cùng trỏ một Ollama: hàng đợi và khóa này nằm trong một process, chưa có backplane/khóa phân tán. Không khóa được chương trình khác gọi Ollama trực tiếp.

## API và bảo vệ

- `POST /bridge/jobs`: Bearer REMOTE_AI_KEY; body `{id: UUID, operation: "rewrite"|"evaluate", rewrite: RewriteRequest}` hoặc `evaluation: {text, profile, images}`. Trả202 và snapshot nhanh, không giữ kết nối chờ model.
- `GET /bridge/jobs/{id}`: cùng khóa, trả queued/running/completed/failed/cancelled, vị trí trong danh sách chờ và result/failure. Không trả caption/ảnh đầu vào. Không có API liệt kê job của các máy.
- `DELETE /bridge/jobs/{id}`: chỉ bỏ yêu cầu queued. Running trả409 và tiếp tục đến khi inference/unload thực sự kết thúc. Không giải phóng model gate sớm.
- `/bridge/health`: `queueEnabled:true`, `queueVersion:1`, `queue.waiting`, `queue.running`, capacity và giới hạn thời gian/bộ nhớ; chỉ qua khóa API. Các route UI/API dự án vẫn bị chặn tại cổng gateway.
- Endpoint cũ `/bridge/evaluate` và `/bridge/rewrite` vẫn hoạt động, nhưng cùng đi qua FIFO và cùng khóa AI. Chúng giữ kết nối chờ nên client cũ vẫn có thể gặp timeout Cloudflare khi hàng đợi dài. Cần cập nhật cả máy client để dùng job/polling.
- Giới hạn submit30/phút và poll/cancel600/phút trong gateway process; dữ liệu tối đa30MB/request, validation caption/profile/ảnh giữ nguyên. Phản hồi no-store. Không log payload/ảnh/key.

## Thứ tự và lỗi

`AiGatewayQueue` dùng một reader FIFO; evaluate, rewrite và đọc ảnh đều vào cùng hàng. Worker chỉ nhận lượt tiếp theo sau khi action hoàn tất hoặc lỗi, kể cả phần giải phóng model. Action dùng `AiRunner.AcquireAsync` cùng semaphore với gọi AI local, không có khóa AI thứ hai độc lập. Một máy có thể đang cào trong khi chờ; backend của nó chỉ nhận kết quả AI sau lượt được xử lý.

Trước tạo crawl, gateway báo busy nhưng có queueEnabled thì không chặn riêng vì busy; vẫn kiểm tra online/model và cấu hình cào. Hàng đợi đầy hoặc AI lỗi sau đó có thể khiến lượt cào paused theo cơ chế hiện có, không báo hoàn tất giả.

Client mới (`AiQueuedClient`) tạo UUID, submit một lần, poll khoảng3giây, tối đa45phút. Khi mất phản hồi submit, tra lại cùng UUID, không POST lặp. Không tự gửi lại inference khi GET mất kết nối/gateway restart. GET được phép thử tiếp tối đa3 lỗi tạm liên tiếp; queue full/auth/output lỗi không tự retry submit. Nếu gateway cũ trả404 cho POST/jobs thì mới fallback API cũ (quy tắc retry evaluate cũ vẫn chỉ áp dụng đường fallback này).

Kết quả failed giữ mã lỗi (ví dụ CLEF_INVALID_OUTPUT, AI_UNCHANGED_OUTPUT422, AI_QUEUE_WAIT_TIMEOUT504). Không lưu draft thành công khi output lỗi. ID giống nhau + cùng payload trong thời gian lưu trả cùng job, không chạy lần hai; ID giống nhưng payload khác trả409. Không bảo đảm idempotency sau khi gateway restart hoặc bản ghi kết quả hết TTL.

## Giới hạn mặc định, có thể đặt trong .env cho Compose

| Biến | Mặc định | Ý nghĩa |
|---|---:|---|
| AI_QUEUE_MAX_JOBS | 8 | Tổng số đang chạy + đang chờ; clamp1–64 |
| AI_QUEUE_MAX_PAYLOAD_BYTES | 67108864 | Ngân sách payload serialized của job chưa xong; không phải giới hạn tổng RAM của model/process |
| AI_QUEUE_WAIT_TIMEOUT_SECONDS | 900 | Hết15phút khi chưa bắt đầu thì failed, không gọi model; clamp1–3600 |
| AI_QUEUE_RESULT_TTL_SECONDS | 600 | Giữ kết quả/lỗi10phút sau hoàn tất; clamp1–3600 |

Có tối đa256 bản ghi trạng thái/kết quả mặc định (`AiQueue:MaxRetainedJobs`, clamp1–4096). Bỏ/hoàn tất job giải phóng closure/payload; result còn đến TTL. Các cấu hình tương ứng là `AiQueue:MaxJobs/MaxPayloadBytes/WaitTimeoutSeconds/ResultTtlSeconds`.

Hàng đợi **trong RAM, không bền qua restart**. Stop bỏ các job chưa chạy; restart mất job/kết quả, client nhận AI_JOB_NOT_FOUND và không tự replay. Đang chạy không bị đánh dấu đã hủy để mở model tiếp theo. Giới hạn chờ không phải hard timeout cho toàn inference; AiRunner giữ timeout HTTP và unload hiện có. Không restart gateway khi còn lượt AI quan trọng; kiểm tra queue/busy trước bảo trì.

Đây không phải khóa tác vụ cào/tài khoản giữa nhiều máy. Vấn đề ownership và đoạn startup sửa task trong DB chung vẫn là phạm vi riêng, chưa được sửa trong đợt này.

## Kiểm tra

```powershell
docker build -f tests/AiQueueChecks/Dockerfile -t sammicrawl-ai-queue-checks .
docker run --rm sammicrawl-ai-queue-checks
```

Fixture dùng controller/pipeline/queue/gate thật, fake inference và fake HTTP phía client. Không DB, cookie Facebook, model hoặc API Facebook. Kiểm tra FIFO nhiều client, khóa chung, lỗi/tiếp tục, bỏ job chờ, expiry, memory budget, idempotency, auth và client không submit lặp. Không coi fixture là đo tải thực tế hoặc chất lượng model.
