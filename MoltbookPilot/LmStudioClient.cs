using MoltbookPilot.Models;

namespace MoltbookPilot
{
    public class LmStudioClient(HttpClient http)
    {
        public async Task<ChatCompletionsResponse.Message?> CreateAsync(
            string model,
            List<ChatMessage> messages,
            IReadOnlyList<ToolDefinition>? tools = null,
            CancellationToken ct = default)
        {
            var payload = new
            {
                model = model,
                messages = messages,
                tools = tools,
                tool_choice = tools is null ? null : "auto",
                temperature = 0.2
            };

            var resp = await http.PostAsJsonAsync("/v1/chat/completions", payload, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<MoltbookPilot.Models.ChatCompletionsResponse>(cancellationToken: ct);
            return json?.choices?.FirstOrDefault()?.message;

        }

        public async Task<string> ChatAsync(
            string model,
            string system,
            string user,
            CancellationToken ct = default)
        {
            var messages = new List<ChatMessage>
        {
            new() { role = "system", content = system },
            new() { role = "user", content = user }
        };

            var msg = await CreateAsync(model, messages, tools: null, ct);
            return msg?.content ?? string.Empty;
        }
    }
}
