using MoltbookPilot.Services;

public sealed class MoltbookEngagementHostedService(
    IServiceScopeFactory scopes,
    IConfiguration cfg,
    EngagementStatusStore status)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = int.TryParse(cfg["Moltbook:Engage:IntervalMinutes"], out var m) ? m : 5;
        var postId = cfg["Moltbook:Engage:PostId"];

        while (!stoppingToken.IsCancellationRequested)
        {
            status.LastRunUtc = DateTime.UtcNow;
            status.LastError = "";

            if (string.IsNullOrWhiteSpace(postId))
            {
                status.LastResult = "No PostId configured (Moltbook:Engage:PostId).";
                await Task.Delay(TimeSpan.FromMinutes(60), stoppingToken);
                continue;
            }

            try
            {
                using var scope = scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<MoltbookComposeService>();

                status.LastResult = await svc.EngagePostCommentsOnceAsync(postId, stoppingToken);
            }
            catch (Exception ex)
            {
                status.LastError = ex.Message;
                status.LastResult = "Failed";
            }

            await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
        }
    }
}
