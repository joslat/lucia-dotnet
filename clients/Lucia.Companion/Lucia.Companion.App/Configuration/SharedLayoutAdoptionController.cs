using System.Net;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Configuration;

/// <summary>Protected metadata review and confirmation. The parent owns its draft and the later local Save.</summary>
public sealed class SharedLayoutAdoptionController(ICompanionConnection service, ConnectionSession identity,
    Func<HomeTopology> currentDraft, TimeProvider? clock = null) : IDisposable
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private CancellationTokenSource? requestCancellation;
    private int generation;
    private bool disposed;
    private long waitStarted;
    private TimeSpan serverWait;
    public SharedLayoutAdoptionPlan? Plan { get; private set; }
    public HomeTopology Draft => currentDraft();
    public bool IsBusy { get; private set; }
    public bool CanRead => !disposed && !IsBusy;
    public bool RequiresConnectionReview { get; private set; }
    public string StatusText { get; private set; } = "Read the shared setup to compare it with this draft. Nothing is saved or replaced by reading.";
    public Task PendingOperation { get; private set; } = Task.CompletedTask;
    public event Action? Changed;

    public Task ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRead) { return PendingOperation; }
        PendingOperation = ReadCoreAsync(confirm: false, cancellationToken);
        return PendingOperation;
    }

    public Task<HomeTopology?> AddToDraftAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRead || Plan is null) { return Task.FromResult<HomeTopology?>(null); }
        var operation = ReadCoreAsync(confirm: true, cancellationToken);
        PendingOperation = operation;
        return operation;
    }

    private async Task<HomeTopology?> ReadCoreAsync(bool confirm, CancellationToken cancellationToken)
    {
        if (time.GetElapsedTime(waitStarted) < serverWait)
        {
            Plan = null;
            StatusText = "Lucia asked this device to wait before another read. Try Read shared setup again after the server wait. Your draft is unchanged.";
            Notify(); return null;
        }
        var reviewed = Plan;
        var startedGeneration = generation;
        if (!confirm) { Plan = null; }
        IsBusy = true; RequiresConnectionReview = false;
        StatusText = confirm ? "Rechecking access and the inspected shared version. Your draft is unchanged." : "Checking access and reading the permitted shared setup.";
        Notify();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCancellation = cancellation;
        try
        {
            var latest = await service.ReadSharedLayoutAsync(identity, cancellation.Token);
            if (disposed || startedGeneration != generation) { return null; }
            cancellation.Token.ThrowIfCancellationRequested();
            if (latest is null)
            {
                Plan = null;
                StatusText = "No shared setup is available in this device's permitted view. Keep editing locally or ask the owner to configure it. Your draft is unchanged.";
                return null;
            }
            if (confirm)
            {
                var accepted = reviewed!.ApplyTo(currentDraft(), latest, identity);
                Plan = null;
                StatusText = "Shared metadata checked. Add it to the draft, then review and Save setup on this device.";
                return accepted;
            }
            Plan = SharedLayoutAdoptionPlan.Create(currentDraft(), latest, identity);
            StatusText = latest.HasUnqualifiedBindings
                ? "Review the permitted setup. Some source bindings are unqualified and omitted; no missing report is inferred. Adding replaces this draft, not the saved setup."
                : "Compare both setups. Adding replaces this draft only. Save setup is a separate choice.";
            serverWait = TimeSpan.Zero;
            return null;
        }
        catch (Exception error)
        {
            if (error is CompanionConnectionException { RetryAfter: { } delay }) { waitStarted = time.GetTimestamp(); serverWait = delay; }
            else if (error is CompanionConnectionException { StatusCode: HttpStatusCode.TooManyRequests }) { waitStarted = time.GetTimestamp(); serverWait = TimeSpan.FromMinutes(1); }
            if (disposed || startedGeneration != generation) { return null; }
            Plan = null;
            RequiresConnectionReview = error is SessionStateException or SessionStorageException
                or CompanionConnectionException { Failure: ConnectionFailure.Unauthorized }
                or CompanionConnectionException { StatusCode: HttpStatusCode.Forbidden };
            StatusText = error switch
            {
                SessionStateException or SessionStorageException => "The protected connection changed or needs recovery. Open Connection before reading shared setup again. Your draft is kept.",
                CompanionConnectionException { Failure: ConnectionFailure.Unauthorized } or CompanionConnectionException { StatusCode: HttpStatusCode.Forbidden }
                    => "This device is no longer authorized. Open Connection to review access. Shared metadata is hidden; your draft is kept.",
                CompanionConnectionException { StatusCode: HttpStatusCode.TooManyRequests } => "Lucia asked this device to wait. Nothing was added. Try reading again after the server wait.",
                CompanionConnectionException { StatusCode: HttpStatusCode.ServiceUnavailable } => "Shared setup cannot be qualified right now. Check Lucia's source connection, then read again. Your draft is kept.",
                InvalidOperationException => "The draft or shared setup changed. Read shared setup again and review the latest versions before adding. Nothing was replaced.",
                OperationCanceledException => "The read stopped. Read shared setup again before adding anything. Your draft is kept.",
                _ => "Shared setup could not be verified. Check network, HTTPS trust and source configuration, then read again. Your draft is kept."
            };
            return null;
        }
        finally
        {
            requestCancellation = null; IsBusy = false; Notify();
        }
    }

    public void Invalidate()
    {
        generation++; requestCancellation?.Cancel(); Plan = null;
        StatusText = "Shared setup hidden. Read it again to recheck access before adding anything. Your draft is kept.";
        Notify();
    }
    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true; generation++; requestCancellation?.Cancel(); Plan = null; Changed = null;
    }
    private void Notify() { if (!disposed) { Changed?.Invoke(); } }
}
