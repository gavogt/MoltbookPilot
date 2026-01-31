namespace MoltbookPilot.Models
{
    public sealed class ChatMessage
    {
        public string role { get; set; } = "";
        public string? content { get; set; }

        // For tool role messages
        public string? tool_call_id { get; set; }
        public string? name { get; set; }
    }
}
