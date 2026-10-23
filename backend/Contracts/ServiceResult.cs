namespace SammiCrawl.Backend.Contracts;

public sealed record ServiceResult(int Status, object? Value = null)
{
    public static ServiceResult Ok(object? value = null) => new(200, value);
    public static ServiceResult BadRequest(object? value = null) => new(400, value);
    public static ServiceResult Unauthorized() => new(401);
    public static ServiceResult NotFound(object? value = null) => new(404, value);
    public static ServiceResult Conflict(object? value = null) => new(409, value);
    public static ServiceResult StatusCode(int status) => new(status);
    public static ServiceResult Json(object value, int statusCode) => new(statusCode, value);
}
