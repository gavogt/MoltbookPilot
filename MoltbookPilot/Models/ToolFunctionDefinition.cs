namespace MoltbookPilot.Models
{
    public class ToolFunctionDefinition
    {
        public string name { get; set; } = "";
        public string description { get; set; } = "";
        public object parameters { get; set; } = new { };
    }
}
