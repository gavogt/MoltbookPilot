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

    public sealed record CommentReq(string postId, string content, string? parentId);

    public sealed record EngageOnceReq(string postId);

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewReq req, CancellationToken ct)
    {
        try
        {
            var state = await store.GetOrCreateAsync(ct);
            if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            {
                // ✅ return JSON, not plain text
                return BadRequest(new { error = "No API key saved. Join/claim first." });
            }

            var take = req.take <= 0 ? 15 : Math.Min(req.take, 50);

            var (draft, debug) = await compose.GenerateDraftAsync(
                submolt: req.submolt,
                take: take,
                userContext: req.userContext ?? "",
                ct: ct);

            // ✅ JSON
            return Ok(new PreviewResp(draft, debug));
        }
        catch (Exception ex)
        {
            // ✅ JSON even when something throws
            return StatusCode(500, new { error = ex.Message, detail = ex.ToString() });
        }
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

    [HttpPost("upvote/post/{postId}")]
    public async Task<IActionResult> UpvotePost([FromRoute] string postId, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return BadRequest("No API key saved. Join/claim first.");

        var result = await compose.UpvotePostAsync(postId, ct);
        return Content(result, "text/plain");
    }

    [HttpPost("upvote/comment/{commentId}")]
    public async Task<IActionResult> UpvoteComment([FromRoute] string commentId, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return BadRequest("No API key saved. Join/claim first.");

        var result = await compose.UpvoteCommentAsync(commentId, ct);
        return Content(result, "text/plain");
    }

    [HttpPost("comment")]
    public async Task<IActionResult> Comment([FromBody] CommentReq req, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return BadRequest("No API key saved. Join/claim first.");

        var result = await compose.CommentOnPostAsync(req.postId, req.content, req.parentId, ct);
        return Content(result, "text/plain");
    }

    // ✅ manual “do the automation once” endpoint for testing
    [HttpPost("engage-once")]
    public async Task<IActionResult> EngageOnce([FromBody] EngageOnceReq req, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return BadRequest("No API key saved. Join/claim first.");

        var result = await compose.EngagePostCommentsOnceAsync(req.postId, ct);
        return Content(result, "text/plain");
    }
}
