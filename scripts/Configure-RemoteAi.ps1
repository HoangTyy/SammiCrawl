param([string]$BaseUrl = 'https://ollama.khanhdc.id.vn')
$ErrorActionPreference = 'Stop'
$uri = [Uri]$BaseUrl
if (-not $uri.IsAbsoluteUri -or $uri.Scheme -ne 'https') { throw 'BaseUrl phải dùng HTTPS.' }
$secret = Read-Host 'Secret key do chủ máy AI cấp' -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
    $key = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ($key.Length -lt 32 -or $key -match '[\r\n]') { throw 'Secret key không hợp lệ.' }
    $envPath = Join-Path (Split-Path $PSScriptRoot -Parent) '.env'
    $lines = if (Test-Path -LiteralPath $envPath) { @(Get-Content -LiteralPath $envPath) } else { @() }
    $lines = @($lines | Where-Object { $_ -notmatch '^REMOTE_AI_(BASE_URL|API_KEY)=' })
    $lines += "REMOTE_AI_BASE_URL=$($BaseUrl.TrimEnd('/'))"
    $lines += "REMOTE_AI_API_KEY=$key"
    [IO.File]::WriteAllLines($envPath, $lines, [Text.UTF8Encoding]::new($false))
    Write-Host 'Đã lưu cấu hình client trong .env. Chạy: docker compose up -d --build app'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $key = $null
    $secret.Dispose()
}
