# Security checks

Chạy từ thư mục gốc repo:

```powershell
docker build -f tests/SecurityChecks/Dockerfile -t sammicrawl-security-checks .
docker run --rm sammicrawl-security-checks
```

Hoặc khi có .NET 10 SDK: `dotnet run --project tests/SecurityChecks/SecurityChecks.csproj` (cần port 8081 local trống). Ưu tiên Docker để cách ly cổng và filesystem.

Bộ kiểm tra liên kết code production của pipeline, đăng ký security, AuthController và helper download. Auth service là fixture không có DB. Cookie/key giả chỉ tồn tại trong container; media nằm trong thư mục tạm; HTTP handler giả không ra mạng. Không expose cổng container và không đọc .env/appsettings/key volume thật. Chương trình thoát khác 0 nếu có assert thất bại.

43 assert bao phủ static public/private GET/HEAD/range/cache, đường dẫn media chuẩn hóa, token CSRF gắn với danh tính/cookie, DELETE không body, gateway port/key, phạm vi WorkerKey, AuthController body limit, auth rate limit/Retry-After, giới hạn stream và cleanup/lưu file hoàn tất. Các lần login sai là auth service giả, không phải tài khoản thật.

Không bao phủ truy vấn EF/quyền nhiều tenant, Facebook/CDN/DNS thật, chất lượng AI/prompt injection benchmark, TLS/Cloudflare, secret rotation, tấn công phân tán hoặc giới hạn pixel/giải nén. CSP ở lượt này chỉ có frame-ancestors/object-src/base-uri; không coi là full CSP chống XSS.
