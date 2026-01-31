using System.Text.Json;

namespace MoltbookPilot.Models;

public static class ToolLoop
{
    public static async Task<string> RunToolLoopAsync(
        LmStudioClient lm,
        AgentTools toolsRuntime,
        string model,
        List<ChatMessage> messages,
        IReadOnlyList<ToolDefinition>? toolDefs,
        int maxSteps = 25,
        CancellationToken ct = default)
    {
        for (var step = 0; step < maxSteps; step++)
        {
            var msg = await lm.CreateAsync(model, messages, toolDefs, ct);
            if (msg is null)
                return "No response from model.";

            // Add the assistant message (content + tool_calls if present)
            messages.Add(new ChatMessage
            {
                role = "assistant",
                content = msg.content,
                tool_calls = msg.tool_calls
            });

            // If there are no tool calls, we’re done
            if (msg.tool_calls is null || msg.tool_calls.Length == 0)
                return msg.content ?? string.Empty;

            // Execute each tool call & add tool result messages
            foreach (var call in msg.tool_calls)
            {
                var toolName = call.function?.name ?? "";
                var argsJson = call.function?.arguments;
                if (string.IsNullOrWhiteSpace(argsJson)) argsJson = "{}";

                string result = await ExecuteToolAsync(toolsRuntime, toolName, argsJson, ct);

                messages.Add(new ChatMessage
                {
                    role = "tool",
                    name = toolName,
                    tool_call_id = call.id,  // IMPORTANT
                    content = result
                });
            }
        }

        return $"Stopped after {maxSteps} steps without finishing.";
    }

    private static async Task<string> ExecuteToolAsync(
        AgentTools toolsRuntime,
        string toolName,
        string argsJson,
        CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            return toolName switch
            {
                "http_get" => await toolsRuntime.HttpGetAsync(
                    root.GetProperty("url").GetString()!, ct),

                "http_post_json" => await toolsRuntime.HttpPostJsonAsync(
                    root.GetProperty("url").GetString()!,
                    root.GetProperty("body"),
                    root.TryGetProperty("headers", out var h) ? JsonToDict(h) : null,
                    ct),

                _ => $"Error: Unknown tool '{toolName}'"
            };
        }
        catch (Exception ex)
        {
            return $"Error executing tool '{toolName}': {ex.Message}";
        }
    }

    private static Dictionary<string, string> JsonToDict(JsonElement el)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in el.EnumerateObject())
            dict[p.Name] = p.Value.GetString() ?? "";
        return dict;
    }
}
