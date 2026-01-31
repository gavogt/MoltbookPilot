namespace MoltbookPilot.Models
{
    public class ToolCall
    {
        public string id { get; set; } = "";
        public string type { get; set; } = "function";
        public ToolCallFunction? function { get; set; }
    }
}
