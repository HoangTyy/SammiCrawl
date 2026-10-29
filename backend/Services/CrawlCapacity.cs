namespace SammiCrawl.Backend.Services;

public static class CrawlCapacity
{
    // Each account is used once per task; this is a ceiling, not a promise of results.
    public static int MaximumPosts(int accountCount, int accountPostLimit) =>
        (int)Math.Min(1000L, Math.Max(0L, accountCount) * Math.Max(0L, accountPostLimit));

    public static string? Validate(int requested, int accountCount, int accountPostLimit)
    {
        var maximum = MaximumPosts(accountCount, accountPostLimit);
        return requested > maximum
            ? $"Tổng số bài tối đa không được vượt {maximum} bài: {accountCount} tài khoản cào × {accountPostLimit} bài/tài khoản (tối đa 1000 bài/tác vụ). Giảm tổng số bài, tăng giới hạn mỗi tài khoản hoặc bổ sung tài khoản đủ điều kiện."
            : null;
    }
}
