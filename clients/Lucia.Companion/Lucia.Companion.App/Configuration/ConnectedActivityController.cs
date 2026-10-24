using System.Net;
using Lucia.Companion.Connection;

namespace Lucia.Companion.App.Configuration;

/// <summary>In-memory activity reads and lifecycle. Does not own Home reports, setup, credentials or a timer.</summary>
public sealed class ConnectedActivityController : IDisposable
{
    private readonly ICompanionConnection service;
    private readonly ConnectionSession identity;
    private readonly TimeProvider clock;
    private long lastAttempt;
    private TimeSpan refreshDelay = TimeSpan.FromSeconds(5);
    private TimeSpan serverDelay;
    private int failures;
    private int generation;
    private bool automaticRead;
    private bool suspended;
    private bool disposed;
    public ConnectedActivityController(ICompanionConnection service, ConnectionSession identity, TimeProvider? clock = null)
    {
        this.service = service; this.identity = identity; this.clock = clock ?? TimeProvider.System;
        AccessDenied = !identity.Session.CanReadActivity;
        StatusText = AccessDenied ? "Activity was not approved for this device. Its Home access is separate." : "Activity has not been checked.";
    }
    public ActivitySnapshot? Snapshot { get; private set; }
    public bool IsBusy { get; private set; }
    public bool CanRefresh => !disposed && !suspended && !IsBusy && !AuthorityLost && identity.Session.CanReadActivity
        && clock.GetElapsedTime(lastAttempt) >= serverDelay;
    public bool AuthorityLost { get; private set; }
    public bool AccessDenied { get; private set; }
    public string StatusText { get; private set; } = "Activity has not been checked.";
    public Task PendingOperation { get; private set; } = Task.CompletedTask;
    public event Action? Changed;
    public Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsBusy) { return PendingOperation; }
        if (suspended || !identity.Session.CanReadActivity || AuthorityLost) { return Task.CompletedTask; }
        if (!CanRefresh)
        {
            StatusText = "Lucia asked this device to wait before checking activity again."; Notify();
            return Task.CompletedTask;
        }
        PendingOperation = ReadAsync(cancellationToken);
        return PendingOperation;
    }

    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        var startedGeneration = generation;
        IsBusy = true; AccessDenied = false; automaticRead = true; serverDelay = TimeSpan.Zero; lastAttempt = clock.GetTimestamp();
        StatusText = "Checking authorized activity."; Notify();
        try
        {
            var received = await service.ReadActivityAsync(identity, cancellationToken);
            if (disposed || suspended || generation != startedGeneration) { return; }
            received.RequireGrant(identity.Session);
            if (Snapshot is { } prior && received.Requests.Any(current => prior.Requests.FirstOrDefault(old => old.RequestId == current.RequestId) is { } old && InvalidContinuation(old, current)))
            { throw new CompanionConnectionException(ConnectionFailure.InvalidResponse); }
            Snapshot = received;
            failures = 0; refreshDelay = TimeSpan.FromSeconds(5); automaticRead = received.IsConfigured;
            StatusText = !received.IsConfigured ? "Activity collection is not configured for this member on this host. Ask the owner to review attribution."
                : received.Requests.Count == 0 ? "No retained attributed requests. This does not mean Lucia or the household is idle."
                : "Recent requests from your approved sources.";
        }
        catch (Exception error)
        {
            // A late throttle still constrains foreground recovery after suspension.
            if (!disposed && error is CompanionConnectionException temporary && IsTransient(temporary))
            {
                lastAttempt = clock.GetTimestamp();
                serverDelay = temporary.RetryAfter ?? (temporary.StatusCode == HttpStatusCode.TooManyRequests ? TimeSpan.FromMinutes(1) : TimeSpan.Zero);
            }
            if (disposed || suspended || generation != startedGeneration) { return; }
            Snapshot = null;
            AccessDenied = error is ActivityAccessException;
            AuthorityLost = error is SessionStateException or CompanionConnectionException { Failure: ConnectionFailure.Unauthorized }
                or CompanionConnectionException { StatusCode: HttpStatusCode.Forbidden };
            automaticRead = error is CompanionConnectionException retryable && IsTransient(retryable);
            if (automaticRead)
            {
                refreshDelay = TimeSpan.FromSeconds(Math.Min(300, 20 * Math.Pow(2, Math.Min(failures++, 4))));
                if (serverDelay > refreshDelay) { refreshDelay = serverDelay; }
                lastAttempt = clock.GetTimestamp();
            }
            StatusText = error switch
            {
                ActivityAccessException => "Activity is not permitted for this device. Home access stays separate. Ask the owner to review activity access.",
                SessionStateException => "The protected connection changed or needs recovery. Open Connection before viewing activity again.",
                CompanionConnectionException { Failure: ConnectionFailure.Unauthorized } or CompanionConnectionException { StatusCode: HttpStatusCode.Forbidden }
                    => "The device session is no longer authorized. Open Connection to recover access.",
                SessionStorageException => "Protected connection storage could not be read safely. Open Connection to review recovery.",
                CompanionConnectionException { StatusCode: HttpStatusCode.TooManyRequests } => "Lucia asked this device to wait before checking activity again.",
                OperationCanceledException => "The activity check stopped. Refresh to check current access and requests.",
                _ => "Activity could not be verified. Previous requests are hidden; no sample activity was substituted."
            };
            if (automaticRead) { StatusText += " Retrying automatically while this view is active."; }
        }
        finally { IsBusy = false; Notify(); }
    }

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || suspended) { return; }
        if (automaticRead && !IsBusy && clock.GetElapsedTime(lastAttempt) >= refreshDelay)
        { await RefreshAsync(cancellationToken); }
    }

    public void Suspend()
    {
        if (disposed) { return; }
        suspended = true; generation++; Snapshot = null;
        StatusText = "Activity hidden. Current access will be checked when the view resumes."; Notify();
    }
    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await PendingOperation;
        if (disposed) { return; }
        suspended = false;
        if (!identity.Session.CanReadActivity)
        { StatusText = "Activity was not approved for this device. Its Home access is separate."; Notify(); return; }
        await RefreshAsync(cancellationToken);
    }
    private static bool IsTransient(CompanionConnectionException error) => error.Failure == ConnectionFailure.Transport
        || error.Failure == ConnectionFailure.Rejected && error.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
    private static bool InvalidContinuation(ActivityRequest previous, ActivityRequest current)
    {
        // CurrentTargetAreas is freshly qualified registry metadata, not a lifecycle event.
        // A moved/renamed target can change that projection without rewriting the request revision.
        if (current.StartedAt != previous.StartedAt || current.PreviousRequestId != previous.PreviousRequestId || current.UpdatedAt < previous.UpdatedAt
            || current.Revision < previous.Revision || current.Revision == previous.Revision && (current.State != previous.State || current.UpdatedAt != previous.UpdatedAt || current.Category != previous.Category)
            || current.Category != previous.Category && (previous.Category != RequestActivityCategory.General || previous.State is not (RequestActivityState.Received or RequestActivityState.Working)))
        { return true; }
        return previous.State switch
        {
            RequestActivityState.Received => false,
            RequestActivityState.Working => current.State == RequestActivityState.Received,
            RequestActivityState.NeedsInput => current.State is not (RequestActivityState.NeedsInput or RequestActivityState.Continued),
            _ => current.State != previous.State
        };
    }
    private void Notify() { if (!disposed) { Changed?.Invoke(); } }
    public void Dispose() { if (disposed) { return; } Suspend(); disposed = true; Changed = null; }
}
