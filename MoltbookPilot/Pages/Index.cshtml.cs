using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MoltbookPilot.Data;
using MoltbookPilot.Services;

namespace MoltbookPilot.Pages
{
    public class IndexModel : PageModel
    {
        private readonly MoltbookStateStore _store;

        public MoltbookAgentState? Moltbook { get; private set; }

        public IndexModel(MoltbookStateStore store)
        {
            _store = store;
        }

        public async Task OnGetAsync(CancellationToken ct)
        {
            Moltbook = await _store.GetOrCreateAsync(ct);
        }

        public static string Mask(string? value, int showLast = 6)
        {
            if (string.IsNullOrWhiteSpace(value)) return "(none)";
            if(value.Length <= showLast) return new string('*', value.Length);
            return new string('*', value.Length - showLast) + value[^showLast..];
        }
    }
}
