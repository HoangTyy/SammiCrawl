using Microsoft.AspNetCore.Mvc;

namespace SammiCrawl.Backend.Controllers;

internal static class ServiceResultExtensions
{
    public static IActionResult ToActionResult(this ServiceResult result) => result.Value is null
        ? new StatusCodeResult(result.Status)
        : new ObjectResult(result.Value) { StatusCode = result.Status };
}
