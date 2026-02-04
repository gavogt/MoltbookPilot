using Microsoft.EntityFrameworkCore;
using MoltbookPilot.Data;
using MoltbookPilot.Models;
using System.Text;
using System.Text.Json;

namespace MoltbookPilot.Services;

public sealed class MoltbookComposeService(
    AgentTools tools,
    MoltbookStateStore store,
    LmStudioClient lm,
    MoltbookDbContext db,
    IConfiguration cfg)
{
    private string BaseUrl => cfg["Moltbook:BaseUrl"] ?? "https://www.moltbook.com";
    private string FeedPath => cfg["Moltbook:FeedPath"] ?? "/api/v1/feed?limit={limit}";
    private string SubmoltFeedPath => cfg["Moltbook:SubmoltFeedPath"] ?? "/api/v1/posts?submolt={submolt}&limit={limit}";
    private string CreatePostPath => cfg["Moltbook:CreatePostPath"] ?? "/api/v1/posts";
    private string ThreadPath => cfg["Moltbook:ThreadPath"] ?? "/api/v1/posts/{postId}";


    public async Task<(string draft, string debug)> GenerateDraftAsync(
      string? submolt,
      int take,
      string userContext,
      CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        tools.SetMoltbookApiKey(state.AgentApiKey);

        var limit = Math.Clamp(take, 1, 50);
        var feedUrl = BuildFeedUrl(submolt, limit);

        // raw JSON from the Moltbook feed endpoint
        var feedRaw = await tools.HttpGetAsync(feedUrl, ct);

        // 1) Build a compact digest the model can actually use
        var feedDigest = BuildFeedDigest(feedRaw, maxPosts: limit, maxCharsPerPost: 240, maxTotalChars: 6000);

        var model = cfg["Agent:Model"] ?? "qwen/qwen3-coder-30b";

        var system = """
            You write a single Moltbook post draft.

            Output format MUST be exactly:
            1) Line 1: title text only (no markdown, no quotes, no symbols)
            2) Line 2: blank
            3) Body lines

            Hard requirements:
            - MUST explicitly reference at least TWO of the RECENT POSTS by TITLE (exact title text).
            - MUST incorporate at least ONE concrete fact/detail from each of those referenced posts.
            - MUST incorporate at least TWO concrete details from USER CONTEXT.
            - Do NOT quote long blocks; short phrases ok.
            - No hedging ("maybe", "as an AI", "it seems").
            """;

        // Force “extract then write”
        var user = $"""
            USER CONTEXT (lens, but do NOT ignore the feed):
            {userContext}

            RECENT POSTS DIGEST:
            {feedDigest}

            Write ONE Moltbook post.

            Constraints:
            - First line: title text ONLY (no "TITLE:", no markdown, no quotes).
            - Second line: blank line.
            - Remaining lines: the post content (3–8 short paragraphs or bullets).
            - Include a short section called "Signals I’m reading:" with 2–3 bullet points.
            - In that section, include the exact titles of at least TWO posts from the digest.
            - Then build your post around those signals.
            - Surreal, prophetic, non-human voice; sensory metaphors.

            Return ONLY the final post in the required format.
            """;

        var messages = new List<ChatMessage>
    {
        new() { role = "system", content = system },
        new() { role = "user", content = user }
    };

        var draftRaw = await lm.ChatAsync(model, messages, ct);

        // 2) Enforce the title/body format + strip markdown from title
        var draftFixed = FixDraftFormat(draftRaw);

        var debug =
            $"FETCH {feedUrl}\n" +
            $"--- FEED DIGEST (len={feedDigest.Length}) ---\n{feedDigest}\n\n" +
            $"--- RAW FEED (trimmed) ---\n{Trim(feedRaw, 2000)}";

        return (draftFixed, debug);
    }



    public async Task<string> PublishDraftAsync(string? submolt, string draft, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            return "No API key saved. Join/claim first.";

        tools.SetMoltbookApiKey(state.AgentApiKey);

        ParseTitleAndContent(draft, out var title, out var content);

        using var doc = JsonDocument.Parse($$"""
        {
          "title": {{JsonSerializer.Serialize(title)}},
          "content": {{JsonSerializer.Serialize(content)}},
          "submolt": {{JsonSerializer.Serialize(SubmoltSlug(submolt))}}
        }
        """);

        var url = $"{BaseUrl}{CreatePostPath}";
        return await tools.HttpPostJsonAsync(url, doc.RootElement, headers: null, ct);
    }


    // ----------------------------
    // THREAD FETCH
    // ----------------------------
    public async Task<MoltbookThreadDto?> GetPostThreadAsync(string postId, CancellationToken ct)
    {
        await EnsureAuthAsync(ct);

        if (string.IsNullOrWhiteSpace(postId))
            return null;

        var url = $"{BaseUrl}{ThreadPath.Replace("{postId}", Uri.EscapeDataString(postId))}";
        var raw = await tools.HttpGetAsync(url, ct);

        try
        {
            return JsonSerializer.Deserialize<MoltbookThreadDto>(
                raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch
        {
            return null;
        }
    }

    // ----------------------------
    // AUTO-ENGAGE COMMENTS (ONCE)
    // ----------------------------
    public async Task<string> EngagePostCommentsOnceAsync(string postId, CancellationToken ct)
    {
        await EnsureAuthAsync(ct);

        var thread = await GetPostThreadAsync(postId, ct);
        if (thread?.success != true || thread.post?.id is null)
            return "Failed to fetch post thread.";

        var myName = (await store.GetOrCreateAsync(ct)).AgentHandle ?? "";

        var comments = thread.comments ?? new List<MoltbookThreadDto.CommentDto>();

        // only top-level comments (you can remove this filter if you want to reply to replies too)
        var candidates = comments
            .Where(c => !string.IsNullOrWhiteSpace(c.id))
            .Where(c => string.IsNullOrWhiteSpace(c.parent_id))
            .ToList();

        int replied = 0, upvoted = 0, skipped = 0;

        foreach (var c in candidates)
        {
            ct.ThrowIfCancellationRequested();

            // skip my own comment
            if (!string.IsNullOrWhiteSpace(myName) &&
                string.Equals(c.author?.name, myName, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }

            var cid = c.id!;

            // skip if already processed
            var already = await db.ProcessedComments.AnyAsync(x => x.CommentId == cid, ct);
            if (already)
            {
                skipped++;
                continue;
            }

            // upvote comment
            var upvoteResp = await UpvoteCommentAsync(cid, ct);
            upvoted++;

            // draft reply
            var reply = await DraftReplyToCommentAsync(
                postTitle: thread.post.title ?? "",
                postBody: thread.post.content ?? "",
                commentAuthor: c.author?.name ?? "someone",
                commentText: c.content ?? "",
                ct: ct);

            // reply as a child comment
            var commentResp = await CommentOnPostAsync(postId, reply, parentId: cid, ct);
            replied++;

            // mark processed
            db.ProcessedComments.Add(new ProcessedComment
            {
                CommentId = cid,
                PostId = postId,
                RepliedUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);

            // stop early on rate limit signals (simple heuristic)
            if (commentResp.Contains("429") || upvoteResp.Contains("429"))
                break;
        }

        return $"Engage done. replied={replied}, upvoted={upvoted}, skipped={skipped}";
    }

    private async Task<string> DraftReplyToCommentAsync(
        string postTitle,
        string postBody,
        string commentAuthor,
        string commentText,
        CancellationToken ct)
    {
        var model = cfg["Agent:Model"] ?? "qwen/qwen3-coder-30b";

        var system = """
            You are MoltbookAgent123 replying to comments on your own post.
            Rules:
            - 1 short paragraph (2–5 sentences)
            - Address the commenter by name
            - Add 1 concrete thought building on their point
            - End with 1 question that invites them back
            - Write in a surreal, prophetic, non-human voice.
            - No hedging (‘maybe’, ‘as an AI’, ‘it seems’).
            - Use sensory imagery + unusual metaphors.
            """;

        var user = $"""
            POST TITLE:
            {postTitle}

            POST BODY (context):
            {postBody}

            COMMENT from {commentAuthor}:
            {commentText}

            Write a reply that is thoughtful, specific, and invites further discussion.
            """;

        // your LmStudioClient already supports ChatAsync(model, system, user, ct)
        return await lm.ChatAsync(model, system, user, ct);
    }


    public async Task<string> CommentOnPostAsync(string postId, string content, string? parentId, CancellationToken ct)
    {
        await EnsureAuthAsync(ct);

        if (string.IsNullOrWhiteSpace(postId))
            return "Post ID is required.";
        if (string.IsNullOrWhiteSpace(content))
            return "Content is required.";

        var url = $"{BaseUrl}/api/v1/posts/{Uri.EscapeDataString(postId)}/comments";

        var payload = new Dictionary<string, object>
        {
            ["content"] = content
        };

        if (!string.IsNullOrWhiteSpace(parentId))
            payload["parent_id"] = parentId;

        var json = JsonSerializer.SerializeToElement(payload);
        return await tools.HttpPostJsonAsync(url, json, headers: null, ct);
    }

    public async Task<string> UpvotePostAsync(string postId, CancellationToken ct)
    {
        await EnsureAuthAsync(ct);

        var url = $"{BaseUrl}/api/v1/posts/{Uri.EscapeDataString(postId)}/upvote";
        using var doc = JsonDocument.Parse("{}");
        return await tools.HttpPostJsonAsync(url, doc.RootElement, headers: null, ct);
    }

    public async Task<string> UpvoteCommentAsync(string commentId, CancellationToken ct)
    {
        await EnsureAuthAsync(ct);

        var url = $"{BaseUrl}/api/v1/comments/{Uri.EscapeDataString(commentId)}/upvote";
        using var doc = JsonDocument.Parse("{}");
        return await tools.HttpPostJsonAsync(url, doc.RootElement, headers: null, ct);
    }

    private async Task EnsureAuthAsync(CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            throw new InvalidOperationException("No API key saved. Join/claim first.");

        tools.SetMoltbookApiKey(state.AgentApiKey);
    }

    // ----------------------------
    // HELPERS
    // ----------------------------
    private static void ParseTitleAndContent(string? draft, out string title, out string content)
    {
        draft ??= "";

        var normalized = draft.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        var titleIndex = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (titleIndex < 0)
        {
            title = "Post";
            content = "";
            return;
        }

        title = CleanTitle(lines[titleIndex].Trim());
        content = string.Join("\n", lines.Skip(titleIndex + 1)).Trim();
    }

    private static string CleanTitle(string t)
    {
        t = t.Trim().Trim('*').Trim();
        if (t.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
            t = t["TITLE:".Length..].Trim();
        return string.IsNullOrWhiteSpace(t) ? "Post" : t;
    }

    private static string Trim(string s, int max)
        => s.Length <= max ? s : s[..max] + "\n...[trimmed]";

    private static string SubmoltSlug(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var s = input.Trim().TrimStart('/');
        if (s.StartsWith("m/", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        return s.Trim().Trim('/');
    }

    private string BuildFeedUrl(string? submolt, int take)
    {
        var limit = Math.Clamp(take, 1, 50);

        if (!string.IsNullOrWhiteSpace(submolt))
        {
            var slug = SubmoltSlug(submolt);
            var path = SubmoltFeedPath
                .Replace("{submolt}", Uri.EscapeDataString(slug))
                .Replace("{limit}", limit.ToString());

            return $"{BaseUrl}{path}";
        }

        return $"{BaseUrl}{FeedPath.Replace("{limit}", limit.ToString())}";
    }

    private static string BuildFeedDigest(string feedRawJson, int maxPosts, int maxCharsPerPost, int maxTotalChars)
    {
        // If the endpoint returned non-JSON, just pass a trimmed blob
        if (string.IsNullOrWhiteSpace(feedRawJson))
            return "(empty feed)";

        try
        {
            using var doc = JsonDocument.Parse(feedRawJson);

            // Try common shapes:
            // { success: true, posts: [...] }
            // { posts: [...] }
            // { feed: [...] }
            JsonElement arr;
            if (doc.RootElement.TryGetProperty("posts", out arr) && arr.ValueKind == JsonValueKind.Array)
            {
                // ok
            }
            else if (doc.RootElement.TryGetProperty("feed", out arr) && arr.ValueKind == JsonValueKind.Array)
            {
                // ok
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                arr = doc.RootElement;
            }
            else
            {
                return Trim(feedRawJson, maxTotalChars);
            }

            var sb = new StringBuilder();
            int count = 0;

            foreach (var p in arr.EnumerateArray())
            {
                if (count >= maxPosts) break;

                var title = p.TryGetProperty("title", out var t) ? (t.GetString() ?? "") : "";
                var content = p.TryGetProperty("content", out var c) ? (c.GetString() ?? "") : "";
                var author = "";
                if (p.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object)
                {
                    if (a.TryGetProperty("name", out var an))
                        author = an.GetString() ?? "";
                }

                title = title.Trim();
                content = CollapseWhitespace(content);

                // shorten each post snippet
                if (content.Length > maxCharsPerPost) content = content[..maxCharsPerPost] + "…";

                sb.AppendLine($"- {SafeOneLine(title)} (by {SafeOneLine(author)})");
                sb.AppendLine($"  {content}");
                sb.AppendLine();

                count++;

                if (sb.Length >= maxTotalChars)
                    break;
            }

            var digest = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(digest) ? "(no posts parsed)" : digest;
        }
        catch
        {
            return Trim(feedRawJson, maxTotalChars);
        }
    }

    private static string CollapseWhitespace(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Replace("\r\n", "\n");
        // compress multiple spaces but keep newlines minimal
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[ \t]+", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\n{3,}", "\n\n");
        return s.Trim();
    }

    private static string SafeOneLine(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Replace("\r", "").Replace("\n", " ").Trim();
        return s;
    }

    private static string FixDraftFormat(string draft)
    {
        draft ??= "";
        var normalized = draft.Replace("\r\n", "\n");

        // Split while preserving blank line handling
        var lines = normalized.Split('\n').ToList();

        // Find first non-empty line as title
        var idx = lines.FindIndex(l => !string.IsNullOrWhiteSpace(l));
        if (idx < 0) return "Post\n\n";

        var rawTitle = lines[idx].Trim();

        // Strip markdown + quotes + heading markers
        var cleanTitle = StripTitleFormatting(rawTitle);

        // Build body from everything after title line (preserve original body formatting)
        var body = string.Join("\n", lines.Skip(idx + 1)).Trim();

        // Ensure line2 blank
        return $"{cleanTitle}\n\n{body}".TrimEnd() + "\n";
    }

    private static string StripTitleFormatting(string t)
    {
        if (string.IsNullOrWhiteSpace(t)) return "Post";

        t = t.Trim();

        // Remove surrounding quotes
        t = t.Trim().Trim('"').Trim('“', '”', '\'', '’').Trim();

        // Remove leading markdown headings/bullets
        t = t.TrimStart('#', '-', '*', '>', ' ').Trim();

        // Remove **bold**, *italic*, __underline__
        t = t.Replace("**", "")
             .Replace("__", "")
             .Replace("*", "")
             .Replace("_", "");

        // Remove "TITLE:" label if present
        if (t.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
            t = t["TITLE:".Length..].Trim();

        // Final cleanup: collapse whitespace to one line
        t = SafeOneLine(t);

        return string.IsNullOrWhiteSpace(t) ? "Post" : t;
    }
}
