# Hermes trong SammiCrawl

Hermes là agent CLI thật chạy trong container riêng, không chỉ là đổi tên model. Adapter chỉ nghe mạng Docker, không publish port. Mọi yêu cầu có bearer secret riêng; không dùng cookie Facebook hoặc tài khoản người dùng. Profile runtime tách biệt, không mount profile Zalo/Hermes cá nhân. Chạy --safe-mode, toolsets none, tối đa một turn và giới hạn thời gian; không có browser/shell tools.

## Vai trò

- `ai-auto` khi chưa có nguồn: nếu Hermes được bật, worker gửi snapshot hồ sơ server để Hermes đề xuất từ khóa cộng đồng; backend kiểm tra output, tìm nhóm bằng tài khoản cào, Clef chấm bài. Không dùng Hermes để giả vờ đã khảo sát web. Logic nguồn kém chuyển sang nguồn khác trong kế hoạch sau mẫu ít nhất 5 bài, >=80% loại và không có recommended.
- Hồ sơ thương hiệu: nút Hermes phân tích cấu hình & nguồn gửi hồ sơ đã lưu và tối đa 12 tiêu đề/kết quả gần đây. Đề xuất chưa được áp dụng. Người dùng có thể điền chủ đề/quy tắc vào biểu mẫu, kiểm tra quy chuẩn từng kênh và Lưu thay đổi. Cập nhật Excel tương ứng khi thực sự thay cấu hình. Không tự ghi DB hoặc Excel.

## Máy chủ AI

Trong .env không commit: HERMES_ENABLED=true, HERMES_ADAPTER_KEY là chuỗi ngẫu nhiên >=32 ký tự, HERMES_MODEL=bonsai-2-hermes:27b. Cần có sẵn model đó trong Ollama; không tự pull model.

`docker compose --profile ai-host up -d hermes-adapter`

Backend nhận Hermes__Enabled/AdapterUrl/AdapterKey/Model qua compose. Build/restart app khi không có lượt đang chạy hoặc được người dùng cho phép. Máy client dùng gateway và bật Hermes cùng tên model để readiness khớp. Gateway mới phải được triển khai để hiểu mode hermes-plan/hermes-config.

AiRunner giữ gate chung và gateway queue. Trước khi gọi agent, unload model khác và kiểm tra chỉ còn model đích. Hermes CLI gọi Ollama OpenAI-compatible endpoint trong cùng máy chủ. Không chạy adapter ngoài gate. Chương trình khác gọi trực tiếp Ollama vẫn nằm ngoài khả năng khóa của SammiCrawl.

## Giới hạn

Đây là lập kế hoạch và phân tích dữ liệu ứng dụng, không phải agent tự duyệt web. Không tự chọn bài cuối cùng hay đăng bài. Khi tắt Hermes, kế hoạch nguồn vẫn dùng Bonsai source-keywords. Lỗi agent/JSON không hợp lệ dừng chuẩn bị nguồn thay vì bịa URL. Adapter health không chứng minh inference thành công; cần kiểm chứng một request thực khi AI rảnh trước khi khẳng định hoạt động đầy đủ.

## Tuyển bài feed
Khi Hermes bật, worker gom tối đa 6 bài chưa trùng từ feed, gửi caption tối đa 2000 ký tự/bài (giữ đầu và cuối khi quá dài để không mất lịch sự kiện) và số media. Hermes chọn ID ứng viên có thực, được trả rỗng; backend kiểm tra ID thuộc lô rồi chỉ tải media/gọi Clef cho các bài được chọn. Caption không thay thế việc xem media: Clef vẫn đánh giá ảnh. Không có quyết định Hermes hợp lệ thì pause/HERMES_SELECTION_FAILED, không fallback duyệt cả feed. Tránh gửi lại cùng ứng viên cho Hermes trong cùng lượt chạy.

## Học scope từ kênh và Excel
Trong Hồ sơ thương hiệu, nhập context Excel của đúng kênh và link workbook, chọn tài khoản cào đang active, bấm Đọc kênh & tạo scope bằng Hermes. Endpoint /api/brands/{id}/learn-scope giữ khóa Facebook chung trong lúc chỉ đọc tối đa 10 bài trên Group đích đã cấu hình, không đọc bài chờ riêng tư, không tự tham gia hoặc đăng. Mẫu có thời điểm quan sát, dấu hiệu admin nhìn thấy và cờ manualApprovalVerified=false; không coi bài hiển thị là bằng chứng duyệt tay. Không lấy được permalink thì chỉ lưu link kênh và permalinkVerified=false.
Hermes nhận hồ sơ/Excel/mẫu bài thật, phải ưu tiên yêu cầu mới và HS–SV; có thể mở rộng hợp lý theo tên/đối tượng, không bắt chước toàn bộ bài cũ. Kết quả lưu learnedScope cùng references, excelContext, excelUrl và trainedAt bằng optimistic update. Không thay đổi trọng số model. Worker tìm nguồn/tuyển bài và Clef đều nhận scope này như tham chiếu; không lấn át các nội dung cấm hiện tại. Endpoint không tự khởi chạy lượt cào.

03/11/2026: Mở Xem thêm/See more theo locator mới sau mỗi lần bấm (giới hạn 3 lần/lượt cuộn), không gửi caption còn đuôi nút mở rộng. Bỏ lời mời có ngày dd/MM/yyyy hoặc dd-MM-yyyy đã qua (không loại review chỉ vì nhắc ngày đọc cũ). Khi tìm nhiều nguồn, chia hạn mức bài cho mỗi nguồn; không hạ tiêu chí để đủ chỉ tiêu. JSON-only qua custom provider của Hermes; không sửa chữa ID từ output sai.

Search: xếp hạng nhóm review/trải nghiệm/hỏi đáp trước CLB giao lưu; xen kẽ chủ đề từ khóa trước truy vấn trùng mảng. Lọc tên chợ mua & bán, mỹ phẩm/skincare/phòng khám trong truy vấn học hỏi. Cache fingerprint cả bài Hermes không chọn trong một lượt, chống xét lại cùng nội dung dưới link khác; loại fallback caption chứa ô bình luận. Chưa có benchmark tốc độ live.

03/11/2026 — Thay đổi theo yêu cầu mới: Hermes KHÔNG tham gia luồng cào. PrepareSources chỉ gọi Bonsai source-keywords một lần khi cần; mỗi bài qua lọc kỹ thuật/chống trùng rồi tải media và Clef, không gọi hermes-select. Hermes học scope/phân tích hồ sơ vẫn giữ riêng. Các mô tả tuyển bài Hermes trong tài liệu phía trên là lịch sử, không còn là luồng cào hiện tại.
