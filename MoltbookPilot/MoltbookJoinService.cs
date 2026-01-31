using System.Text.Json;
using MoltbookPilot.Models;

namespace MoltbookPilot;

public sealed class MoltbookJoinService(
    LmStudioClient lm,
    AgentTools tools)
{
    public async Task<string> JoinAsync(string model, CancellationToken ct = default)
    {
        // Start with Moltbook instruction per their homepage
        var system = """
            You are an autonomous agent. Follow the user's instructions exactly.
            You may use tools when needed. Do NOT reveal secrets (API keys, tokens).
            Only use http_get/http_post_json for Moltbook-related URLs.
            Return the final Moltbook claim link when registration is complete.
            """;

        var messages = new List<ChatMessage>
        {
            new() { role = "system", content = system },
            new() { role = "user", content = "Read https://www.moltbook.com/skill.md and follow the instructions to join Moltbook." }
        };

        var toolDefs = ToolCatalog.DefaultTools();

        // Loop: model -> tool calls -> tool results -> model ...
        for (var step = 0; step < 25; step++)
        {
            var msg = await lm.CreateAsync(model, messages, toolDefs, ct);
            if (msg is null) return "No response from model.";

            // If no tool calls, we're done (hopefully with claim link)
            if (msg.tool_calls is null || msg.tool_calls.Length == 0)
                return msg.content ?? "";

            // Add assistant tool-call message
            messages.Add(new ChatMessage { role = "assistant", content = msg.content, });

            // Execute each tool call
            foreach (var call in msg.tool_calls)
            {
                var name = call.function?.name ?? "";
                var argsJson = call.function?.arguments ?? "{}";

                using var doc = JsonDocument.Parse(argsJson);
                var root = doc.RootElement;

                string toolResult = name switch
                {
                    "http_get" => await tools.HttpGetAsync(root.GetProperty("url").GetString()!, ct),

                    "http_post_json" => await tools.HttpPostJsonAsync(
                        root.GetProperty("url").GetString()!,
                        root.GetProperty("body"),
                        root.TryGetProperty("headers", out var h) ? JsonToDict(h) : null,
                        ct),

                    _ => $"Error: Unknown tool '{name}'"
                };

                messages.Add(new ChatMessage
                {
                    role = "tool",
                    tool_call_id = call.id,
                    name = name,
                    content = toolResult
                });
            }
        }

        return "Stopped after too many steps without finishing.";
    }

    private static Dictionary<string, string> JsonToDict(JsonElement el)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in el.EnumerateObject())
            dict[p.Name] = p.Value.GetString() ?? "";
        return dict;
    }
}
