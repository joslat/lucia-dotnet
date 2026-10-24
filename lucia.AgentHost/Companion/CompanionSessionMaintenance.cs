using lucia.Agents.Companion;

namespace lucia.AgentHost.Companion;

public sealed class CompanionSessionMaintenance(ICompanionSessionService sessions, ICompanionPairingService pairings,
    TimeProvider clock, ILogger<CompanionSessionMaintenance> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), clock);
        do
        {
            try
            {
                await sessions.PruneExpiredAsync(stoppingToken).ConfigureAwait(false);
                await pairings.PruneExpiredAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Do not log credential-bearing commands or connection data. Authentication still rejects expired sessions.
                logger.LogWarning("Companion session cleanup failed; it will retry. Expired credentials remain unusable.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
