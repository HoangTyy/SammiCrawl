# SammiCrawl — nghiên cứu bảo mật (đang chuẩn bị)

Ngày 25/10/2026, Asia/Ho_Chi_Minh. Chủ dự án yêu cầu Hermes nghiên cứu ở context156K để nâng cấp bảo mật. **Chưa chạy được lượt Hermes có kiểm chứng context156K**: có worker Zalo ngoài dự án liên tục gọi Qwen; đang chờ quyết định tạm dừng an toàn vì đây cũng là tiến trình xử lý chiến dịch, không phải AI độc lập.

## Chuẩn bị đã hoàn tất

- Snapshot44 file code có số dòng (~156k ký tự), không gồm appsettings/.env/DB/cookie/APIkey hay dữ liệu khách hàng. Prompt local: work/security-research/hermes-security-request.txt, bịgitignore.
- Profile Hermes nghiên cứu riêng đã đặt context_length/ollama_num_ctx159744 (=156Ki tokens). Không sửa profile/gateway Hermes chính hoặc cấu hình OpenClaw.
- Đã thử nạp model ngắn, nhưng Qwen được tiến trình khác gọi thay thế trước khi xác minh api/ps của Hermes; **không kết luận156K hoàn toànGPU chỉ từ config**. Chưa chạy đồng thời lượt nghiên cứu mới với chiến dịch.
- Không commit/push, không đăng Facebook, không thay DB hoặc chiến dịch Zalo.

## Kết quả sơ bộ của Codex — không gán cho Hermes

| Phát hiện | Bằng chứng | Phạm vi/giới hạn |
|---|---|---|
| Media crawl chưa cần đăng nhập | RequestPipelineExtensions gọi UseStaticFiles trước Authentication/Authorization; HEAD một JPEG có sẵn khôngcookie trả200 image/jpeg | Xác nhận trênlocalhost5080. Chưa kết luận media mở qua publicdomain; tunnelAI có chặn đường khác bridge. Cần auth trước staticmedia và no-store. |
| Login chưa có throttle | Program chỉ đăng ký remote-ai limiter; AuthController Login/Setup không policy hạn chế | Xác nhậncode, không brute-force vào DB/liveuser. Cần limiter và testfixture. |
| Cookie có HttpOnly/SameSiteStrict nhưng chưa antiforgerytoken | frontend api.ts không token; AddAntiforgery chưa có | SameSiteStrict/JSON/noCORS là bảo vệ hiện có; chưa chứng minh exploit cross-site. Token thêm là defense-in-depth, phải cập nhật frontend và giữ bridge/workerBearer. |
| Secret cấu hình và demo công khai | Tài liệu repo xác nhận quyết địnhtrackconnection trước đây | Không insecret, không đổi/rotatekhôngđượcduyệt, không rewriteGit. Cần quyếtđịnh chủrepo trướcpublicdeployment. |
| Quyền mọiuser trongmộtproject | [Authorize] chung, chưacóownerACL | Đây là sharedprojecthiện tại, không tựgọiIDOR nếu chưa xácđịnh yêu cầu tenant/role. |

npm audit --omit=dev:0 advisory cho dependencyproduction tại lượt kiểm tra; không bảo đảm không có lỗ hổng hoặc bao phủ .NET/devdependency.

## Nguồn chính thức đã trực tiếp đối chiếu

- [Microsoft static file authorization](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/static-files?view=aspnetcore-10.0): static middleware đặt trước authorization không kiểm tra quyền.
- [Microsoft antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0): token/header cho ứng dụng cookie.
- [OWASP Authentication](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html): giới hạn lần thử đăng nhập.
- [OWASP LLM Prompt Injection](https://cheatsheetseries.owasp.org/cheatsheets/LLM_Prompt_Injection_Prevention_Cheat_Sheet.html): nội dungcào/ảnh là dữ liệukhông đángtin; quyền/hànhđộng và phêduyệt phảiđược backendkiểmsoát, không dựa vào prompt.

## Bước tiếp theo

1. Xử lý quyết định tạm dừng worker Zalo ngoài phạm vi SammiCrawl; không kill/suspend thô giữa tác vụ gửi.
2. Nạp Hermes159744tokens, kiểmchứng api/ps context và size/size_vram; chỉ một modelresident. Nếu offload thì dừng, báochủmáy.
3. Chạy nghiên cứu bằng snapshotcode và từngnguồn riêng; lưu kếtquả đúngscope, đốichiếu độc lập với code/runtime.
4. Chỉ nâng cấp phát hiện đã xácnhận, dùng fixture riêng không sửa DBthật. Chưa triển khai bản vá trong bước chuẩn bị này.

## 25/10 — Zalo đã tạm dừng, Hermes156K đang chạy

Người dùng đã xác nhận rõ tạm dừng Zalo để ưu tiên việc này. Đã gọi pauseCampaign chính thức cho3 campaignworker đang cólock, trạng thái paused/reasonowner_pause. Không kill giữa tác vụ, không thay recipient/sent/unknown, không tự resume. Không còn tiến trình zalo-campaign-cli hoạt động khi kiểm tra lại. Audit riêng ở work/security-research/zalo-paused-audit.json (không chứa thông tin khách).

Lượt nghiên cứu đầu khởi tạo ở98304 dù YAML ghi159744: phát hiện tham số num_ctx98304 được đóng trong model gốc và profile custom dùng OpenAI-compatible API. Đã dừng đúng tiến trình nghiên cứu riêng đó, không dùng kết quả lượt sai context.

Tạo aliaslocal sammicrawl-security-hermes:156k từ bonsai-2-hermes:27b, chỉ đổi parameter num_ctx159744, không đổi modelgốc hay Hermesprofilechính. Profile nghiên cứu riêng chọnalias này. NguồnAPIcreate: https://docs.ollama.com/api/create .

Lượt thật session20261025_080639_ac7042, log work/security-research/hermes-security-156k.jsonl. Live/api/ps: chỉ1model, context_length159744; size19911388690=size_vram19911388690bytes. HaiRTX3060 dùng khoảng10.8GB+9.65GB theo nvidia-smi. Đây là modelplacement, không cónghĩatổngRAMhệthống bằng0. Nghiên cứu đang chạy; chưa có kết luận củaHermes hoặc bảnvátriểnkhai.

**Trạng thái mới nhất:** đã có báo cáo chốt và bản hardening local ở SECURITY-HARDENING.md; các đoạn chưa chạy/đang chờ ở trên là nhật ký cũ. SessionHermes156K chốt exit0; lượt thu thậpnguồntrướcexit1cóbảnphântích, đãđượcphảnbiện, khôngcoiđó làquétthànhcông. Xem báo cáo chốt cho43assert, smokeHTTP/UI, bảnvá vàgiớihạn.
