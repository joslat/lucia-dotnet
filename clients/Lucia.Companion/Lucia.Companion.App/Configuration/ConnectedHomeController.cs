using System.Net;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Configuration;

/// <summary>Verified reads, report lifetime and metadata coordination. UI composition stays in the workspace.</summary>
public sealed class ConnectedHomeController : IDisposable
{
    private readonly ICompanionConnection service;
    private readonly ConnectionSession identity;
    private readonly HomeViewModel state;
    private readonly HomeProfileSession persistence;
    private readonly TimeProvider clock;
    private HomeSnapshot? snapshot;
    private string? revision;
    private long reportRequestStarted;
    private long lastAttempt;
    private TimeSpan refreshDelay = TimeSpan.FromSeconds(20);
    private TimeSpan serverDelay;
    private int failures;
    private bool automaticRead;
    private int generation;
    private bool suspended;
    private bool disposed;
    public bool HasData { get; private set; }
    public bool IsBusy { get; private set; }
    public bool CanRefresh => !disposed && !suspended && !IsBusy && clock.GetElapsedTime(lastAttempt) >= serverDelay;
    public bool AuthorityLost { get; private set; }
    public string StatusText { get; private set; } = "Reading authorized Home reports.";
    public HomeCatalog? Catalog => snapshot is null ? null : ConnectedHomeProjection.Catalog(snapshot);
    public Task PendingOperation { get; private set; } = Task.CompletedTask;
    public event Action? Changed;

    public ConnectedHomeController(ICompanionConnection service, ConnectionSession identity, HomeViewModel state, HomeProfileSession persistence, TimeProvider? clock = null)
    {
        this.service = service; this.identity = identity; this.state = state; this.persistence = persistence; this.clock = clock ?? TimeProvider.System;
        if (state.IsDemo || state.Home.Id != identity.Session.HomeId) { throw new ArgumentException("A connected workspace requires its verified home identity."); }
        state.PreferencesChanged += Reproject;
    }

    public Task RefreshAsync(bool hideWhileChecking = true, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (suspended) { return Task.CompletedTask; }
        if (IsBusy) { return PendingOperation; }
        if (!CanRefresh)
        {
            StatusText = "Lucia asked this device to wait. Reports remain hidden; retry will happen automatically while this view is active.";
            Notify(); return Task.CompletedTask;
        }
        PendingOperation = ReadAsync(hideWhileChecking, cancellationToken);
        return PendingOperation;
    }

    private async Task ReadAsync(bool hideWhileChecking, CancellationToken cancellationToken)
    {
        var startedGeneration = generation;
        IsBusy = true; AuthorityLost = false; automaticRead = true; serverDelay = TimeSpan.Zero; lastAttempt = clock.GetTimestamp();
        if (hideWhileChecking) { ClearReports(); }
        StatusText = "Checking access and reading Home reports."; Notify();
        try
        {
            await persistence.InitializeAsync(cancellationToken);
            var started = clock.GetTimestamp();
            var update = await service.ReadHomeUpdateAsync(identity, revision, cancellationToken);
            if (disposed || suspended || startedGeneration != generation) { return; }
            if (update.Snapshot is { } received)
            {
                if (received.HomeId != identity.Session.HomeId) { throw new SessionStateException(SessionStateFailure.IdentityChanged); }
                snapshot = received; reportRequestStarted = started;
                state.SetSources(ConnectedHomeProjection.Sources(received));
            }
            else if (snapshot is null || revision != update.Revision)
            { throw new CompanionConnectionException(ConnectionFailure.InvalidResponse); }
            revision = update.Revision;
            failures = 0; refreshDelay = TimeSpan.FromSeconds(20); serverDelay = TimeSpan.Zero;
            HasData = true;
            Reproject();
            state.SetSourceNotice($"Reports captured {snapshot!.CapturedAt.UtcDateTime:yyyy-MM-dd HH:mm 'UTC'} · Refresh every 20 seconds while this view is active. Source reports are not physical verification.");
            StatusText = "Authorized reports loaded. Current means recently reported, not independently verified.";
        }
        catch (Exception error)
        {
            // A late throttled response still controls the next request, even if the app backgrounded meanwhile.
            if (!disposed && error is CompanionConnectionException connectionError && IsTransient(connectionError))
            {
                lastAttempt = clock.GetTimestamp();
                serverDelay = connectionError.RetryAfter ?? (connectionError.StatusCode == HttpStatusCode.TooManyRequests ? TimeSpan.FromMinutes(1) : TimeSpan.Zero);
            }
            if (disposed || suspended || startedGeneration != generation) { return; }
            ClearReports();
            automaticRead = error is CompanionConnectionException retryable && IsTransient(retryable);
            if (automaticRead)
            {
                refreshDelay = TimeSpan.FromSeconds(Math.Min(300, 20 * Math.Pow(2, Math.Min(failures++, 4))));
                if (serverDelay > refreshDelay) { refreshDelay = serverDelay; }
                lastAttempt = clock.GetTimestamp();
            }
            AuthorityLost = error is CompanionConnectionException { Failure: ConnectionFailure.Unauthorized }
                or CompanionConnectionException { StatusCode: HttpStatusCode.Forbidden } or SessionStateException;
            StatusText = error switch
            {
                CompanionConnectionException { Failure: ConnectionFailure.Unauthorized } or CompanionConnectionException { StatusCode: HttpStatusCode.Forbidden }
                    => "This device is no longer authorized. Return to Connection and pair again with the owner's approval.",
                SessionStateException => "The saved connection changed or requires recovery. Return to Connection before opening a home again.",
                CompanionConnectionException { StatusCode: HttpStatusCode.UnprocessableEntity } => "This approved Home is too large for the bounded read. Ask the owner to narrow its places or fields before pairing again.",
                CompanionConnectionException { StatusCode: HttpStatusCode.TooManyRequests } => "Lucia asked this device to wait before retrying.",
                CompanionConnectionException { StatusCode: HttpStatusCode.ServiceUnavailable } => "Home Assistant reports are unavailable. Check Lucia's source connection. Previous reports are hidden.",
                SessionStorageException => "The protected connection could not be read safely. Open Connection to review storage recovery.",
                OperationCanceledException => "The read stopped. Retry to revalidate access and read reports again.",
                _ => "Home reports could not be verified. Check network, HTTPS trust and source configuration, then retry. No previous reports are shown."
            };
            if (automaticRead) { StatusText += " Retrying automatically while this view is open."; }
        }
        finally { IsBusy = false; Notify(); }
    }

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (disposed || suspended) { return; }
        Reproject();
        if (automaticRead && !IsBusy && clock.GetElapsedTime(lastAttempt) >= refreshDelay) { await RefreshAsync(false, cancellationToken); }
    }

    public void Suspend()
    {
        if (disposed) { return; }
        suspended = true; generation++;
        ClearReports();
        StatusText = "Home reports hidden. Access will be checked when this view resumes.";
        Notify();
    }
    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await PendingOperation;
        if (disposed) { return; }
        suspended = false;
        if (AuthorityLost) { Notify(); return; }
        await RefreshAsync(cancellationToken: cancellationToken);
    }
    private void Reproject()
    {
        if (disposed || suspended || snapshot is null || !HasData) { return; }
        // Including the whole read duration is a conservative age bound; phone/server wall clocks need not match.
        state.ReplaceHome(ConnectedHomeProjection.Project(state.Home, snapshot, clock.GetElapsedTime(reportRequestStarted)), notifyPreferences: false);
    }
    private void ClearReports()
    {
        HasData = false; snapshot = null; revision = null;
        state.ClearSelection(); state.SetSources([]);
        state.ReplaceHome(new(state.Home.Id, state.Home.Name, state.Home.Places.Select(place => place with
        { State = PlaceState.Unavailable, Reason = "No verified reports available.", Observations = Array.Empty<Observation>() }), state.Home.Connections, state.Home.Bindings), notifyPreferences: false);
        state.SetSourceNotice("No verified Home reports available.");
    }
    private void Notify() { if (!disposed) { Changed?.Invoke(); } }
    private static bool IsTransient(CompanionConnectionException error) => error.Failure == ConnectionFailure.Transport
        || error.Failure == ConnectionFailure.Rejected && error.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
    public void Dispose()
    {
        if (disposed) { return; }
        Suspend(); disposed = true;
        state.PreferencesChanged -= Reproject;
        Changed = null;
    }
}
