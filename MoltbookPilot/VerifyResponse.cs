namespace MoltbookPilot
{
    public sealed class VerifyResponse
    {
        public bool success { get; set; }
        public bool valid { get; set; }
        public VerifiedAgent? agent { get; set; }
    }
}
