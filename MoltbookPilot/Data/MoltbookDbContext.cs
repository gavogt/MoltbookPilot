using Microsoft.EntityFrameworkCore;

namespace MoltbookPilot.Data
{
    public class MoltbookDbContext : DbContext
    {
        public MoltbookDbContext(DbContextOptions<MoltbookDbContext> options)
            : base(options)
        {
        }

        public DbSet<MoltbookAgentState> MoltbookAgentStates => Set<MoltbookAgentState>();
        public DbSet<ProcessedComment> ProcessedComments => Set<ProcessedComment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ProcessedComment>()
                .HasIndex(x => x.CommentId)
                .IsUnique();
        }
    }
}
