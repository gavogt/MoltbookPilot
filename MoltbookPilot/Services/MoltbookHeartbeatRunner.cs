using MoltbookPilot.Models;

namespace MoltbookPilot.Services
{
    public class MoltbookHeartbeatRunner(
    LmStudioClient lm,
    MoltbookStateStore store,
    AgentTools toolsRuntime)
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(4);

        public async Task<string> RunOnceAsync(string model, CancellationToken ct = default)
        {
            var state = await store.GetOrCreateAsync(ct);

            if (string.IsNullOrWhiteSpace(state.AgentApiKey))
                return "No API key saved. Join/claim first.";

            if (state.LastHeartbeatUtc is not null &&
                DateTime.UtcNow - state.LastHeartbeatUtc.Value < Interval)
            {
                var next = state.LastHeartbeatUtc.Value + Interval;
                return $"Heartbeat not needed yet. Next at {next:u}.";
            }

            var system = """
                You are an autonomous agent running a Moltbook heartbeat.
                Only use tools http_get and http_post_json.
                Only access moltbook.com or www.moltbook.com URLs.
                Do not reveal secrets (API keys).
                """;

            var messages = new List<ChatMessage>
            {
                new() { role = "system", content = system },
                new() { role = "user", content =
                @"Fetch https://www.moltbook.com/heartbeat.md and follow the instructions EXACTLY.
                Do NOT stop after summarizing.
                Execute the checklist using the provided tools.
                If any step requires auth, use the Moltbook API with Authorization automatically (do not print secrets).
                Return a short report of what you executed (endpoints + status codes) and then write 'HEARTBEAT_DONE'." }

            };

            var toolDefs = ToolCatalog.DefaultTools(); // should return IReadOnlyList<ToolDefinition>

            toolsRuntime.SetMoltbookApiKey(state.AgentApiKey);

            var result = await ToolLoop.RunToolLoopAsync(
                lm, toolsRuntime, model, messages, toolDefs, maxSteps: 25, ct: ct);

            var success = result.Contains("HEARTBEAT_DONE", StringComparison.OrdinalIgnoreCase);
            if (success)
                await store.SetLastHeartbeatUtcAsync(DateTime.UtcNow, ct);

            return result;
        }
    }
}