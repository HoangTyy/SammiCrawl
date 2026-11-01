using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace SammiCrawl.Backend.Controllers;
[ApiController,Authorize]
[Route("api/tasks")]
public sealed class TaskControlController(TaskControlService service):ControllerBase
{
    [HttpPost("{id:guid}/pause")] public async Task<IActionResult> Pause(Guid id)=>(await service.Pause(id)).ToActionResult();
    [HttpPost("{id:guid}/resume")] public async Task<IActionResult> Resume(Guid id)=>(await service.Resume(id)).ToActionResult();
}
