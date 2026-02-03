using System.Text.Json;
using MoltbookPilot.Models;

namespace MoltbookPilot.Services;

public sealed class MoltbookComposeService(
    AgentTools tools,
    MoltbookStateStore store,
    LmStudioClient lm,
    IConfiguration cfg)
{
    // Default to www to avoid redirect/auth header weirdness reported by users. 
    private string BaseUrl => cfg["Moltbook:BaseUrl"] ?? "https://www.moltbook.com";

    private string FeedPath => cfg["Moltbook:FeedPath"] ?? "/api/v1/feed?limit={limit}";

    // Collection endpoint approach (works more reliably than /m/{name} routes in your tests)
    private string SubmoltFeedPath => cfg["Moltbook:SubmoltFeedPath"]
        ?? "/api/v1/posts?submolt={submolt}&limit={limit}";

    private string CreatePostPath => cfg["Moltbook:CreatePostPath"] ?? "/api/v1/posts";

    private string PostCommentsPath => cfg["Moltbook:PostCommentsPath"] ?? "/api/v1/posts/{postId}/comments?sort={sort}";

    public async Task<(string draft, string debug)> GenerateDraftAsync(
        string? submolt,
        int take,
        string userContext,
        CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        tools.SetMoltbookApiKey(state.AgentApiKey);

        var feedUrl = BuildFeedUrl(submolt, take);
        var feedRaw = await tools.HttpGetAsync(feedUrl, ct);

        var model = cfg["Agent:Model"] ?? "qwen/qwen3-coder-30b";

        var system = """
            You write a single Moltbook post draft.
            Rules:
            - Do NOT include API keys or secrets.
            - Do NOT quote large blocks verbatim from other posts; summarize and add original value.
            - Keep it concise: title line + short body.
            - Use the user's context as the priority lens.
            """;

        var user = $"""
            USER CONTEXT (must incorporate this; prioritize it over the feed):
            {userContext}

            RECENT POSTS (use as inspiration; do not quote long blocks):
            {feedRaw}

            Write ONE Moltbook post.

            Hard requirements:
            - First line: title text ONLY (no "TITLE:", no markdown, no quotes).
            - Second line: blank line.
            - Remaining lines: the post content (3–8 short paragraphs or bullets).
            - Include at least 2 concrete details from USER CONTEXT (brief quote or paraphrase).
            - React to at least 2 themes from RECENT POSTS.
            - If USER CONTEXT conflicts with RECENT POSTS, prioritize USER CONTEXT.
            - Write in a surreal, prophetic, non-human voice.
            - No hedging (‘maybe’, ‘as an AI’, ‘it seems’).
            - Use sensory imagery + unusual metaphors.

            Return ONLY the post in the required format.
            """;

        var messages = new List<ChatMessage>
        {
            new() { role = "system", content = system },
            new() { role = "user", content = user }
        };

        // NOTE: This assumes your LmStudioClient has ChatAsync(model, messages, ct)
        var draft = await lm.ChatAsync(model, messages, ct);

        var debug = $"FETCH {feedUrl}\n\n---\n{Trim(feedRaw, 4000)}";
        return (draft, debug);
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

    public async Task<string> CommentOnPostAsync(string postId, string content, string? parentId, CancellationToken ct)
    {
        await EnsureAuthAsync(ct);

        if(string.IsNullOrWhiteSpace(postId))
            return "Post ID is required.";

        if(string.IsNullOrWhiteSpace(content))
            return "Content is required.";

        var url = $"{BaseUrl}/api/v1/posts/{Uri.EscapeDataString(postId)}/comments";

        var payload = new Dictionary<string, object>
        {
            ["content"] = content ?? ""
        };

        if(!string.IsNullOrWhiteSpace(parentId))
        {
            payload["parent_id"] = parentId;
        }

        var json = JsonSerializer.SerializeToElement(payload);
        return await tools.HttpPostJsonAsync(url, json, headers: null, ct);

    }

    private async Task EnsureAuthAsync(CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        if (string.IsNullOrWhiteSpace(state.AgentApiKey))
            throw new InvalidOperationException("No API key saved. Join/claim first.");
        tools.SetMoltbookApiKey(state.AgentApiKey);
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

    // ✅ FIXED: preserve original formatting (blank lines + paragraphs)
    private static void ParseTitleAndContent(string? draft, out string title, out string content)
    {
        draft ??= "";

        var normalized = draft.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        // first non-empty line is title
        var titleIndex = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (titleIndex < 0)
        {
            title = "Post";
            content = "";
            return;
        }

        var rawTitle = lines[titleIndex].Trim();
        title = CleanTitle(rawTitle);

        // content is everything AFTER the title line, preserving blank lines
        content = string.Join("\n", lines.Skip(titleIndex + 1)).Trim();

        // If model returned just a title, keep content empty (or give a placeholder if you prefer)
        if (string.IsNullOrWhiteSpace(content))
            content = "";
    }

    private static string CleanTitle(string t)
    {
        t = t.Trim();

        // strip markdown bold wrappers
        t = t.Trim('*').Trim();

        if (t.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
            t = t.Substring("TITLE:".Length).Trim();

        return string.IsNullOrWhiteSpace(t) ? "Post" : t;
    }

    private static string Trim(string s, int max)
        => s.Length <= max ? s : s[..max] + "\n...[trimmed]";

    private static string SubmoltSlug(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var s = input.Trim().TrimStart('/');

        // accept "/m/general", "m/general", "general"
        if (s.StartsWith("m/", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(2);

        return s.Trim().Trim('/');
    }
}
