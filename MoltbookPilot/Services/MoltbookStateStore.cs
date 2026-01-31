using MoltbookPilot.Data;
using Microsoft.EntityFrameworkCore;

namespace MoltbookPilot.Services
{
    public sealed class MoltbookStateStore(MoltbookDbContext db)
    {
        public async Task<MoltbookAgentState> GetOrCreateAsync(CancellationToken ct = default)
        {
            var row = await db.MoltbookAgentStates.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
            if (row is not null) return row;

            row = new MoltbookAgentState
            {
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            };

            db.MoltbookAgentStates.Add(row);
            await db.SaveChangesAsync(ct);
            return row;
        }

        public async Task SaveClaimAsync(string claimUrl, CancellationToken ct = default)
        {
            var row = await GetOrCreateAsync(ct);
            row.ClaimUrl = claimUrl;
            row.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        public async Task SaveApiKeyAsync(string apiKey, CancellationToken ct = default)
        {
            var row = await GetOrCreateAsync(ct);
            row.AgentApiKey = apiKey;
            row.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        public async Task SetLastHeartbeatUtcAsync(DateTime whenUtc, CancellationToken ct = default)
        {
            var state = await GetOrCreateAsync(ct); 
            state.LastHeartbeatUtc = whenUtc;
            state.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        public async Task SaveRegistrationAsync(string agentHandle, string claimURL, string apiKey, CancellationToken ct = default)
        {
            var row = await GetOrCreateAsync(ct);
            row.AgentHandle = agentHandle;
            row.ClaimUrl = claimURL;
            row.AgentApiKey = apiKey;
            row.UpdatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

        }


    }
}
