# Nguồn Hermes thực lấy được

Nguồn này được trích từ tool_result web_extract, không phải danh sách URL model tự nêu. Số ký tự là độ dài đoạn trả về, không chứng minh đã đọc toàn trang. Không dùng nội dung SEO ngoài danh sách này làm căn cứ triển khai.

| URL | Số lần có kết quả | Đoạn dài nhất (ký tự) |
|---|---:|---:|
| [https://support.buffer.com/articles/managing-and-approving-draft-posts-57li7M8tDA](https://support.buffer.com/articles/managing-and-approving-draft-posts-57li7M8tDA) | 2 | 693 |
| [https://support.sproutsocial.com/hc/en-us/articles/360000576466-Introduction-to-Publishing](https://support.sproutsocial.com/hc/en-us/articles/360000576466-Introduction-to-Publishing) | 2 | 2324 |
| [https://www.w3.org/WAI/tutorials/forms/grouping/](https://www.w3.org/WAI/tutorials/forms/grouping/) | 1 | 499 |
| [https://support.buffer.com/articles/saving-and-scheduling-draft-posts-CBLXg1yFXp](https://support.buffer.com/articles/saving-and-scheduling-draft-posts-CBLXg1yFXp) | 1 | 2986 |

Codex còn trực tiếp mở W3C [Dialog Modal](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/) để kiểm tra bàn phím; các nguồn đối chiếu riêng được ghi trong SAMMICRAWL-RESEARCH.md.

Chỉ sử dụng phát hiện đúng phạm vi đoạn đã đọc. Ví dụ extract Sprout chủ yếu là navigation, không dùng để khẳng định quyền Facebook, auto-expire hoặc tính năng sâu.

## Log bị cắt — không xem như toàn trang đã đọc

JSON tool_result của một số batch bị logger cắt ở khoảng5000 ký tự, nên bảng trên chỉ gồm4 URL có result JSON parse được trọn vẹn. Các đoạn đầu còn đối chiếu được gồm:
- https://docs.apify.com/actors/running/runs-and-builds
- https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/examples/disclosure-navigation/
- https://www.w3.org/WAI/tutorials/forms/notifications/

URL thứ hai trong một batch có tên trong tool_use chưa đủ chứng minh nội dung trả về nếu phần log đó đã bị cắt. Không khẳng định Hermes đọc đầy đủ toàn bộ các trang chỉ dựa vào URL hoặc câu trả lời cuối.

## Codex trực tiếp mở để bù phần bằng chứng còn thiếu

- [Apify input schema](https://docs.apify.com/actors/development/actor-definition/input-schema/specification/v1): schema định nghĩa đầu vào, validation và UI; giới hạn SammiCrawl vẫn lấy từ code backend của chính dự án.
- [Apify monitoring](https://docs.apify.com/actors/running/monitoring): tham khảo cách phân biệt số liệu run/trạng thái, không sao chép hệ thống cảnh báo của Apify vào app.
- [W3C disclosure navigation](https://www.w3.org/WAI/ARIA/apg/patterns/disclosure/examples/disclosure-navigation/): dùng navigation thông thường, không áp menu role nếu không có hành vi tương ứng.
- [W3C modal dialog](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/): kiểm tra focus/Tab/Escape/restore theo phạm vi ghi trong báo cáo QA.

Đây là phạm vi kiểm chứng nguồn, không phải chứng nhận đã thử toàn bộ công cụ của nhà cung cấp. Không có nguồn web nào được dùng để coi tài khoản Facebook đang bật là chắc chắn có quyền đăng.
