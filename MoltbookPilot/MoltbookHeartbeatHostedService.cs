using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoltbookPilot.Services;

namespace MoltbookPilot;

public sealed class MoltbookHeartbeatHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration cfg,
    ILogger<MoltbookHeartbeatHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<MoltbookHeartbeatRunner>();

                var model = cfg["Agent:Model"] ?? "qwen/qwen3-coder-30b";
                var result = await runner.RunOnceAsync(model, stoppingToken);

                log.LogInformation("Heartbeat: {Result}", result);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Heartbeat failed");
            }
        }
    }
}
