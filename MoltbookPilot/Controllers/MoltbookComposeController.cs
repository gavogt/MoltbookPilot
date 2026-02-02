using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MoltbookPilot.Services;

namespace MoltbookPilot.Controllers;

[ApiController]
[Route("api/moltbook/compose")]
public sealed class MoltbookComposeController(
    MoltbookComposeService compose,
    MoltbookStateStore store)
    : ControllerBase
{
    public sealed record PreviewReq(string? submolt, int take, string? userContext);
    public sealed record PreviewResp(string draft, string debug);

    public sealed record PublishReq(string? submolt, string draft);

    [HttpPost("preview")]
    public async Task<ActionResult<PreviewResp>> Preview([FromBody] PreviewReq req, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return BadRequest("No API key saved. Join/claim first.");

        var take = req.take <= 0 ? 15 : Math.Min(req.take, 50);
        var (draft, debug) = await compose.GenerateDraftAsync(
            submolt: req.submolt,
            take: take,
            userContext: req.userContext ?? "",
            ct: ct);

        return new PreviewResp(draft, debug);
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishReq req, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return BadRequest("No API key saved. Join/claim first.");

        var result = await compose.PublishDraftAsync(req.submolt, req.draft, ct);
        return Content(result, "text/plain");
    }
}
