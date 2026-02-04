using System.ComponentModel.DataAnnotations;

namespace MoltbookPilot.Data
{
    public class ProcessedComment
    {
        public int Id { get; set; }

        [MaxLength(64)]
        public string CommentId { get; set; } = "";

        [MaxLength(64)]
        public string PostId { get; set; } = "";

        public DateTime RepliedUtc { get; set; } = DateTime.UtcNow;

    }
}
