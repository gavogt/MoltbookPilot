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

    // NEW: comment
    public sealed record CommentReq(string postId, string content, string? parentId);

    [HttpPost("comment")]
    public async Task<IActionResult> Comment([FromBody] CommentReq req, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return BadRequest("No API key saved. Join/claim first.");

        var result = await compose.CommentOnPostAsync(req.postId, req.content, req.parentId, ct);
        return Content(result, "text/plain");
    }

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

    // NEW: Upvote a post
    [HttpPost("upvote/post/{postId}")]
    public async Task<IActionResult> UpvotePost([FromRoute] string postId, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return BadRequest("No API key saved. Join/claim first.");

        var result = await compose.UpvotePostAsync(postId, ct);
        return Content(result, "text/plain");
    }

    // NEW: Upvote a comment
    [HttpPost("upvote/comment/{commentId}")]
    public async Task<IActionResult> UpvoteComment([FromRoute] string commentId, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return BadRequest("No API key saved. Join/claim first.");

        var result = await compose.UpvoteCommentAsync(commentId, ct);
        return Content(result, "text/plain");
    }
}
