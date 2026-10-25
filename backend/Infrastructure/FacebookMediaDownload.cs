namespace SammiCrawl.Backend.Infrastructure;

public static class FacebookMediaDownload
{
    public const long MaximumBytes = 50_000_000;
    public static bool Allowed(Uri url) => url.Scheme == "https" && url.Port == 443 && string.IsNullOrEmpty(url.UserInfo)
        && (url.Host.EndsWith(".fbcdn.net", StringComparison.OrdinalIgnoreCase) || url.Host.EndsWith(".facebook.com", StringComparison.OrdinalIgnoreCase));

    public static async Task<long?> CopyBounded(Stream source, Stream target, long limit, CancellationToken cancellation)
    {
        var buffer = new byte[65536];
        long total = 0;
        while (true)
        {
            // Read at most one byte over the limit, even for chunked responses.
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, limit - total + 1)), cancellation);
            if (read == 0) return total;
            total += read;
            if (total > limit) return null;
            await target.WriteAsync(buffer.AsMemory(0, read), cancellation);
        }
    }

    public static async Task<(string FileName, long Bytes)?> Download(HttpClient http, Uri url, string folder, string kind, string cookie, string userAgent, CancellationToken cancellation, long limit = MaximumBytes)
    {
        if (!Allowed(url) || limit < 1 || limit > MaximumBytes) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(cookie)) request.Headers.Add("Cookie", cookie);
        if (!string.IsNullOrEmpty(userAgent)) request.Headers.UserAgent.ParseAdd(userAgent);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > limit) return null;
        var ext = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg", "image/png" => ".png", "image/webp" => ".webp", "video/mp4" => ".mp4", _ => null
        };
        if (ext is null || kind is not ("image" or "video") || (kind == "video") != (ext == ".mp4")) return null;
        Directory.CreateDirectory(folder);
        var name = Guid.NewGuid() + ext;
        var partial = Path.Combine(folder, name + ".partial");
        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            long? copied;
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                copied = await CopyBounded(input, output, limit, timeout.Token);
            if (copied is null || copied == 0) return null;
            File.Move(partial, Path.Combine(folder, name));
            return (name, copied.Value);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
}
