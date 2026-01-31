namespace MoltbookPilot
{
    public class ToolDefinition
    {
        public string type { get; set; } = "function";
        public ToolFunctionDefinition function { get; set; } = new();
    }
}
