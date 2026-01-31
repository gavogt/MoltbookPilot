using MoltbookPilot.Models;

namespace MoltbookPilot;

public static class ToolCatalog
{
    public static List<ToolDefinition> DefaultTools() =>
    [
        new ToolDefinition
        {
            function = new ToolFunctionDefinition
            {
                name = "http_get",
                description = "Fetch a URL over HTTPS and return status + body text.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        url = new { type = "string" }
                    },
                    required = new[] { "url" }
                }
            }
        },
        new ToolDefinition
        {
            function = new ToolFunctionDefinition
            {
                name = "http_post_json",
                description = "POST JSON to a URL with optional headers; returns status + body text.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        url = new { type = "string" },
                        body = new { type = "object" },
                        headers = new
                        {
                            type = "object",
                            additionalProperties = new { type = "string" }
                        }
                    },
                    required = new[] { "url", "body" }
                }
            }
        }
    ];
}
