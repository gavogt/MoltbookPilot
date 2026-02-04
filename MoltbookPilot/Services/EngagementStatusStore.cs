namespace MoltbookPilot.Services
{
    public class EngagementStatusStore
    {
        public DateTime? LastRunUtc { get; set; }
        public string LastResult { get; set; } = "never";
        public string LastError { get; set; } = ""; 
    }
}
