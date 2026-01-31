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
    }
}
