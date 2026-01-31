namespace MoltbookPilot.Data
{
    public class MoltbookAgentState
    {
        public int Id { get; set; }
        public string? AgentHandle { get; set; }
        public string? ClaimUrl { get; set; }
        public string? AgentApiKey { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
