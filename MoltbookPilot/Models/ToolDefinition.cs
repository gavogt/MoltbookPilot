namespace MoltbookPilot.Models
{
    public class ToolDefinition
    {
        public string type { get; set; } = "function";
        public ToolFunctionDefinition function { get; set; } = new();
    }
}
