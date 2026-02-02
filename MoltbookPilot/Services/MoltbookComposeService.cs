using System.Text;
using System.Text.Json;
using MoltbookPilot.Models;

namespace MoltbookPilot.Services;

public sealed class MoltbookComposeService(
    AgentTools tools,
    MoltbookStateStore store,
    LmStudioClient lm,
    IConfiguration cfg)
{

    private string BaseUrl => cfg["Moltbook:BaseUrl"] ?? "https://www.moltbook.com";
    private string FeedPath => cfg["Moltbook:FeedPath"] ?? "/api/v1/feed?limit={limit}";
    private string SubmoltFeedPath => cfg["Moltbook:SubmoltFeedPath"]
        ?? "/api/v1/posts?submolt={submolt}&limit={limit}";
    private string CreatePostPath => cfg["Moltbook:CreatePostPath"] ?? "/api/v1/posts";

    public async Task<(string draft, string debug)> GenerateDraftAsync(
        string? submolt,
        int take,
        string userContext,
        CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        tools.SetMoltbookApiKey(state.AgentApiKey);

        // 1) Fetch posts JSON (best effort)
        var feedUrl = BuildFeedUrl(submolt, take);
        var feedRaw = await tools.HttpGetAsync(feedUrl, ct);

        // 2) Ask LM Studio to draft a post using the feed + your prompt
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

            Return ONLY the post in the required format.
            """;


        var messages = new List<ChatMessage>
        {
            new() { role = "system", content = system },
            new() { role = "user", content = user }
        };

        var draft = await lm.ChatAsync(model, messages, ct);

        var debug = $"FETCH {feedUrl}\n\n---\n{Trim(feedRaw, 4000)}";
        return (draft, debug);
    }

    public async Task<string> PublishDraftAsync(string? submolt, string draft, CancellationToken ct)
    {
        var state = await store.GetOrCreateAsync(ct);
        tools.SetMoltbookApiKey(state.AgentApiKey);

        draft ??= "";
        var lines = draft.Split('\n')
                         .Select(l => l.Trim())
                         .Where(l => !string.IsNullOrWhiteSpace(l))
                         .ToList();

        // Title = first non-empty line, but clean common LLM formatting
        var rawTitle = lines.FirstOrDefault() ?? "Post";
        var title = CleanTitle(rawTitle);

        // Content = rest; if empty, fall back to full draft minus the title line
        var content = string.Join("\n", lines.Skip(1)).Trim();
        if (string.IsNullOrWhiteSpace(content))
            content = draft.Replace(rawTitle, "").Trim();

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

    private static string CleanTitle(string t)
    {
        // remove markdown bold and common "TITLE:" labels
        t = t.Trim().Trim('*').Trim();
        if (t.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
            t = t.Substring("TITLE:".Length).Trim();
        if (t.StartsWith("**TITLE:", StringComparison.OrdinalIgnoreCase))
            t = t.Replace("**", "").Substring("TITLE:".Length).Trim();
        return string.IsNullOrWhiteSpace(t) ? "Post" : t;
    }


    private string BuildFeedUrl(string? submolt, int take)
    {
        var limit = Math.Clamp(take, 1, 50);

        if (!string.IsNullOrWhiteSpace(submolt))
        {
            var slug = SubmoltSlug(submolt); // "general" from "m/general" or "/m/general"
            var path = SubmoltFeedPath
                .Replace("{submolt}", Uri.EscapeDataString(slug))
                .Replace("{limit}", limit.ToString());

            return $"{BaseUrl}{path}";
        }

        return $"{BaseUrl}{FeedPath.Replace("{limit}", limit.ToString())}";
    }

    private static string NormalizeSubmolt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        if (s.StartsWith("m/")) return s;
        if (s.StartsWith("/m/")) return s.TrimStart('/');
        return "m/" + s.TrimStart('/');
    }

    private static string Trim(string s, int max)
        => s.Length <= max ? s : s[..max] + "\n...[trimmed]";

    private static string SubmoltSlug(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var s = input.Trim();

        // accept "/m/general", "m/general", "general"
        s = s.TrimStart('/');
        if (s.StartsWith("m/", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(2);

        // final safety
        return s.Trim().Trim('/');
    }
}
