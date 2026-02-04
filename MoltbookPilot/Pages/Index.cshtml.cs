using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MoltbookPilot.Data;
using MoltbookPilot.Services;

namespace MoltbookPilot.Pages;

public sealed class IndexModel(
    MoltbookDbContext db,
    IConfiguration cfg,
    MoltbookStateStore store)
    : PageModel
{
    public string? PostId { get; private set; }
    public int IntervalMinutes { get; private set; }

    public List<ProcessedComment> RecentProcessed { get; private set; } = [];
    public MoltbookAgentState? Moltbook { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Moltbook = await store.GetOrCreateAsync(ct);

        PostId = cfg["Moltbook:Engage:PostId"];
        IntervalMinutes = int.TryParse(cfg["Moltbook:Engage:IntervalMinutes"], out var m) ? m : 5;

        RecentProcessed = await db.ProcessedComments
            .OrderByDescending(x => x.RepliedUtc)
            .Take(50)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public static string Mask(string? value, int showLast = 6)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(none)";
        if (value.Length <= showLast) return new string('*', value.Length);
        return new string('*', value.Length - showLast) + value[^showLast..];
    }
}
