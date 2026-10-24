using lucia.HomeAssistant.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace lucia.Agents.Companion;

/// <summary>One bounded read against the explicitly bound source, including transient source changes.</summary>
internal static class CompanionSourceRead
{
    public static async Task<T> RunAsync<T>(CompanionOptions settings, IOptionsMonitor<HomeAssistantOptions> sourceOptions,
        TimeProvider clock, ILogger logger, Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        bool MatchesSource(string value)
        {
            try { return settings.SourceBaseUrl.Length > 0 && CompanionOptions.ValidateSourceUrl(value) == CompanionOptions.ValidateSourceUrl(settings.SourceBaseUrl); }
            catch (ArgumentException) { return false; }
        }
        if (!MatchesSource(sourceOptions.CurrentValue.BaseUrl)) { throw new CompanionDataUnavailableException(); }
        var sourceChanged = 0;
        using var changes = sourceOptions.OnChange((current, _) =>
        { if (!MatchesSource(current.BaseUrl)) { Interlocked.Exchange(ref sourceChanged, 1); } });
        using var timeout = new CancellationTokenSource(settings.HomeReadTimeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var result = await read(linked.Token).WaitAsync(linked.Token).ConfigureAwait(false);
            if (Volatile.Read(ref sourceChanged) != 0 || !MatchesSource(sourceOptions.CurrentValue.BaseUrl))
            { throw new CompanionDataUnavailableException(); }
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (CompanionDataUnavailableException) { throw; }
        catch (Exception error)
        {
            logger.LogWarning("Companion source read failed with {FailureType}; no cached household fallback was returned", error.GetType().Name);
            throw new CompanionDataUnavailableException();
        }
    }
}
