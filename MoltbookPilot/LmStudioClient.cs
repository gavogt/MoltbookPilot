namespace MoltbookPilot
{
    public class LmStudioClient(HttpClient http) 
    {
        public async Task<string> ChatAsync (string model, string system, string user, CancellationToken ct = default)
        {
            var payload = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user }

                },
                temperature = 0.7
            };

            var resp = await http.PostAsJsonAsync("/v1/chat/completions", payload, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<ChatCompletionsResponse>(cancellationToken: ct);
            return json?.choices?.FirstOrDefault()?.message?.content ?? string.Empty;

        }
    }
}
