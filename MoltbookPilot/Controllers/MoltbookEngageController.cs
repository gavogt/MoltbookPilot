using Microsoft.AspNetCore.Mvc;
using MoltbookPilot.Services;

namespace MoltbookPilot.Controllers;

[ApiController]
[Route("api/moltbook/engage")]
public sealed class MoltbookEngageController(
    MoltbookComposeService compose,
    IConfiguration cfg)
    : ControllerBase
{
    [HttpPost("run-once")]
    public async Task<IActionResult> RunOnce(CancellationToken ct)
    {
        var postId = cfg["Moltbook:Engage:PostId"];
        if (string.IsNullOrWhiteSpace(postId))
            return BadRequest("Missing Moltbook:Engage:PostId in appsettings.json");

        var result = await compose.EngagePostCommentsOnceAsync(postId, ct);
        return Ok(result);
    }

    [HttpGet("thread")]
    public async Task<IActionResult> GetThread(CancellationToken ct)
    {
        var postId = cfg["Moltbook:Engage:PostId"];
        if (string.IsNullOrWhiteSpace(postId))
            return BadRequest("Missing Moltbook:Engage:PostId in appsettings.json");

        var thread = await compose.GetPostThreadAsync(postId, ct);
        return Ok(thread);
    }
}
