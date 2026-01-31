using System.Text.Json;
using System.Text.Json.Serialization;

namespace MoltbookPilot.Models
{
    public sealed class ChatCompletionsResponse
    {
        public Choice[]? choices { get; set; }

        public sealed class Choice
        {
            public Message? message { get; set; }
        }

        public sealed class Message
        {
            public string? role { get; set; }
            public string? content { get; set; }

            public ToolCall[]? tool_calls { get; set; }
        }
    }
}
