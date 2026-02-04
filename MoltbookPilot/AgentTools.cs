using System.Text;
using System.Text.Json;

namespace MoltbookPilot;

public sealed class AgentTools(HttpClient http)
{
    private string? _moltbookAgentApiKey;

    public void SetMoltbookApiKey(string? apiKey)
    {
        _moltbookAgentApiKey = apiKey;
    }

    private static readonly HashSet<string> AllowedHosts =
        new(StringComparer.OrdinalIgnoreCase) { "www.moltbook.com", "moltbook.com" };

    private static void EnsureAllowed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Invalid URL");

        if (!AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Blocked host: {uri.Host}");
    }

    public async Task<string> HttpGetAsync(string url, CancellationToken ct)
    {
        var target = NormalizeMoltbookUrl(url);
        EnsureAllowed(target.ToString());

        using var req = new HttpRequestMessage(HttpMethod.Get, target);
        AddMoltbookAuthIfNeeded(req);

        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        // If not success, throw with the body so your UI/debug can show it
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"GET {(int)resp.StatusCode}: {body}");

        return body;
    }

    public async Task<string> HttpPostJsonAsync(
        string url,
        JsonElement jsonBody,
        Dictionary<string, string>? headers,
        CancellationToken ct)
    {
        var target = NormalizeMoltbookUrl(url);
        EnsureAllowed(target.ToString());

        using var req = new HttpRequestMessage(HttpMethod.Post, target);

        if (headers is not null)
            foreach (var kv in headers)
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);

        AddMoltbookAuthIfNeeded(req);

        req.Content = new StringContent(
            JsonSerializer.Serialize(jsonBody),
            Encoding.UTF8,
            "application/json");

        using var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"POST {(int)resp.StatusCode}: {body}");

        return body;
    }

    private static string Trim(string s)
        => s.Length <= 4000 ? s : s[..4000] + "\n...[trimmed]";

    private void AddMoltbookAuthIfNeeded(HttpRequestMessage req)
    {
        if (string.IsNullOrWhiteSpace(_moltbookAgentApiKey)) return;

        var host = req.RequestUri?.Host?.ToLowerInvariant();

        if (host is "moltbook.com" or "www.moltbook.com")
        {
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _moltbookAgentApiKey);
        }
    }

    private static Uri NormalizeMoltbookUrl(string url)
    {
        var uri = new Uri(url);

        if (uri.Host.Equals("moltbook.com", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(uri)
            {
                Host = "www.moltbook.com"
            };
            return builder.Uri;
        }

        return uri;
    }
}
