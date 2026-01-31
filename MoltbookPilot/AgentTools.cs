using System.Text;
using System.Text.Json;

namespace MoltbookPilot;

public sealed class AgentTools(HttpClient http)
{
    private static readonly string[] AllowedHosts =
    [
        "www.moltbook.com",
        "moltbook.com"
    ];

    private static void EnsureAllowed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Invalid URL");

        if (!AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Blocked host: {uri.Host}");
    }

    public async Task<string> HttpGetAsync(string url, CancellationToken ct)
    {
        EnsureAllowed(url);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        return $"Status: {(int)resp.StatusCode}\n{Trim(body)}";
    }

    public async Task<string> HttpPostJsonAsync(string url, JsonElement jsonBody, Dictionary<string, string>? headers, CancellationToken ct)
    {
        EnsureAllowed(url);

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (headers is not null)
        {
            foreach (var kv in headers)
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }

        req.Content = new StringContent(JsonSerializer.Serialize(jsonBody), Encoding.UTF8, "application/json");

        var resp = await http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        return $"Status: {(int)resp.StatusCode}\n{Trim(body)}";
    }

    private static string Trim(string s)
        => s.Length <= 4000 ? s : s[..4000] + "\n...[trimmed]";
}
