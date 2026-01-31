namespace MoltbookPilot
{
    public class MoltbookAuthClient(HttpClient http, IConfiguration cfg)
    {
        public async Task<VerifiedAgent?> VerifyAsync(string token, CancellationToken ct = default)
        {
            var appKey = cfg["Moltbook:AppKey"];
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/verify-identity");
            req.Headers.TryAddWithoutValidation("X-Moltbook-App-Key", appKey);
            req.Content = JsonContent.Create(new { token });

            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<VerifyResponse>(cancellationToken: ct);
            return (json?.valid == true) ? json.agent : null;
        }
    }
}
