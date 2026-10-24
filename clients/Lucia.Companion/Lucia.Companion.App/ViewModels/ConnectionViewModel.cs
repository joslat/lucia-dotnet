using Lucia.Companion.Connection;

namespace Lucia.Companion.App.ViewModels;

/// <summary>Connection UX state, never credentials. Screen attachment owns polling ticks; storage/protocol live in Connection.</summary>
public sealed class ConnectionViewModel(ICompanionConnection? service, TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private DateTimeOffset nextPoll;
    public event Action? Changed;
    public string HostAddress { get; set; } = string.Empty;
    public string DeviceName { get; set; } = "Lucia companion";
    public ConnectionViewStage Stage { get; private set; } = ConnectionViewStage.Checking;
    public bool IsBusy { get; private set; }
    public bool IsWaiting { get; private set; }
    public bool IsProblem { get; private set; }
    public string StatusText { get; private set; } = "Checking for a protected connection.";
    public PairingPrompt? Prompt { get; private set; }
    public string ApprovalHost { get; private set; } = string.Empty;
    public string ApprovalDeviceName { get; private set; } = string.Empty;
    public ConnectionSession? Connection { get; private set; }
    public bool IsConnected => Connection is not null && Stage == ConnectionViewStage.Connected;
    public bool HasSavedConnection => service?.HasSavedConnection == true;
    public bool CanStart => service is not null && !IsBusy && !HasSavedConnection && Prompt is null && !IsConnected;
    public bool CanForget => service is not null && !IsBusy && (HasSavedConnection || Stage == ConnectionViewStage.Recovery);
    public string ExpiryText => Prompt is null ? string.Empty : $"Code expires at {Prompt.ExpiresAt.ToLocalTime():t}. Stop waiting pauses checks; it does not cancel an approval already completing.";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy) { return; }
        if (service is null)
        {
            Stage = ConnectionViewStage.Unavailable;
            StatusText = "Protected connection storage is unavailable in this build or could not be initialized. You can explore the clearly labelled demo; no plaintext fallback will be used.";
            Notify(); return;
        }
        IsBusy = true; IsProblem = false; Connection = null; Prompt = null; IsWaiting = false; Stage = ConnectionViewStage.Checking;
        StatusText = "Checking the saved connection with Lucia."; Notify();
        try
        {
            Connection = await service.RestoreAsync(cancellationToken);
            if (service.Endpoint is not null) { HostAddress = service.Endpoint.AbsoluteUri; }
            Stage = Connection is null ? ConnectionViewStage.Form : ConnectionViewStage.Connected;
            StatusText = Connection is null ? "Use your Lucia HTTPS host. No owner key belongs on this device." : "The host verified this device and its approved read access.";
        }
        catch (Exception error) { Fail(error); }
        finally { IsBusy = false; Notify(); }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!CanStart || service is null) { return; }
        Uri endpoint;
        try
        {
            var address = HostAddress.Trim();
            if (!address.Contains("://", StringComparison.Ordinal)) { address = "https://" + address; }
            endpoint = CompanionEndpoint.Validate(new Uri(address, UriKind.Absolute));
            if (string.IsNullOrWhiteSpace(DeviceName) || DeviceName.Trim().Length > 80) { throw new ArgumentException(); }
        }
        catch (Exception error) when (error is ArgumentException or UriFormatException)
        {
            IsProblem = true; StatusText = "Use an HTTPS host without a path, query or embedded key, and a device name of 1 to 80 readable characters."; Notify(); return;
        }
        var submittedName = DeviceName.Trim();
        IsBusy = true; IsProblem = false; Connection = null; StatusText = "Requesting an approval code."; Notify();
        try
        {
            Prompt = await service.BeginAsync(endpoint, submittedName, cancellationToken);
            ApprovalHost = endpoint.AbsoluteUri;
            ApprovalDeviceName = submittedName;
            HostAddress = endpoint.AbsoluteUri;
            nextPoll = clock.GetUtcNow().AddSeconds(Prompt.PollIntervalSeconds);
            Stage = ConnectionViewStage.AwaitingApproval; IsWaiting = true;
            StatusText = "Ask the owner to verify this code and approve this device's exact access.";
        }
        catch (Exception error) { Fail(error); }
        finally { IsBusy = false; Notify(); }
    }

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (!IsWaiting || IsBusy || Prompt is null || service is null) { return; }
        if (Prompt.ExpiresAt <= clock.GetUtcNow()) { EndPrompt("This code expired. Request a new code when the owner is ready."); return; }
        if (clock.GetUtcNow() < nextPoll) { return; }
        IsBusy = true; Notify();
        try
        {
            var result = await service.PollAsync(cancellationToken);
            switch (result.Status)
            {
                case PairingPollStatus.Success:
                    Connection = result.Connection ?? throw new InvalidOperationException("Missing verified connection.");
                    StatusText = IsWaiting ? "The host verified this device and its approved read access." : "Approval completed before waiting stopped. This device is connected; use Disconnect to remove it.";
                    Prompt = null; IsWaiting = false; Stage = ConnectionViewStage.Connected;
                    break;
                case PairingPollStatus.Pending:
                case PairingPollStatus.SlowDown:
                    nextPoll = clock.GetUtcNow() + result.RetryAfter;
                    Stage = IsWaiting ? ConnectionViewStage.AwaitingApproval : ConnectionViewStage.Paused;
                    StatusText = IsWaiting ? result.Status == PairingPollStatus.SlowDown ? "Lucia asked us to wait. The next check will respect its retry delay." : "Waiting for the owner's approval. No home data is available yet."
                        : "Waiting paused. The code remains valid until its expiry.";
                    break;
                case PairingPollStatus.Denied: EndPrompt("The owner declined this request. Check with them before requesting another code."); break;
                case PairingPollStatus.Consumed: EndPrompt("This code was already used. If delivery was interrupted, ask the owner to review connected devices before pairing again."); break;
                default: EndPrompt("This approval code is no longer valid. Request a new one."); break;
            }
        }
        catch (Exception error) { Fail(error); }
        finally { IsBusy = false; Notify(); }
    }

    public void StopWaiting()
    {
        IsWaiting = false;
        if (Prompt is not null)
        {
            Stage = ConnectionViewStage.Paused;
            StatusText = IsBusy ? "Finishing the current check. Any received connection will be protected before it is shown."
                : "Waiting paused. The code remains valid until its expiry.";
        }
        Notify();
    }

    public void ResumeWaiting()
    {
        if (IsBusy || Prompt is null) { return; }
        if (Prompt.ExpiresAt <= clock.GetUtcNow()) { EndPrompt("This code expired. Request a new code."); return; }
        IsWaiting = true; Stage = ConnectionViewStage.AwaitingApproval; StatusText = "Waiting for the owner's approval."; Notify();
    }

    public void ChangeHost()
    {
        if (IsBusy || HasSavedConnection) { return; }
        Prompt = null; IsWaiting = false; Stage = ConnectionViewStage.Form;
        StatusText = "Any abandoned code expires automatically. Use the HTTPS host you intend to trust."; Notify();
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || service is null) { return; }
        IsBusy = true; Connection = null; IsWaiting = false; IsProblem = false;
        StatusText = "Removing the protected connection and requesting revocation."; Notify();
        try
        {
            var confirmed = await service.DisconnectAsync(cancellationToken);
            Prompt = null; Stage = ConnectionViewStage.Form;
            StatusText = confirmed ? "This device connection was revoked."
                : "Connection removed from this device only. Lucia could not confirm revocation; the owner can revoke it from connected devices.";
        }
        catch (Exception error) { Fail(error); }
        finally { IsBusy = false; Notify(); }
    }

    public async Task ForgetAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || service is null) { return; }
        IsBusy = true; Connection = null; IsWaiting = false; Notify();
        try
        {
            await service.ForgetAsync(cancellationToken);
            Prompt = null; Stage = ConnectionViewStage.Form; IsProblem = false;
            StatusText = "Protected connection removed locally only. This does not revoke the device at the host.";
        }
        catch (Exception error) { Fail(error); }
        finally { IsBusy = false; Notify(); }
    }

    private void EndPrompt(string message)
    { Prompt = null; IsWaiting = false; Stage = ConnectionViewStage.Form; IsProblem = true; StatusText = message; Notify(); }
    private void Fail(Exception error)
    {
        Connection = null; Prompt = null; IsWaiting = false; IsProblem = true;
        Stage = HasSavedConnection || error is SessionStorageException ? ConnectionViewStage.Recovery : ConnectionViewStage.Form;
        StatusText = error switch
        {
            SessionStorageException => "The protected connection could not be read or saved safely. Retry after repairing storage, or explicitly forget the local connection. Nothing will be saved as plaintext.",
            CompanionConnectionException { Failure: ConnectionFailure.Unauthorized } => "This device is no longer authorized. Pair again with the owner's approval.",
            CompanionConnectionException { StatusCode: System.Net.HttpStatusCode.TooManyRequests } => "Lucia is limiting requests. Wait a minute before trying again.",
            CompanionConnectionException when HasSavedConnection => "Lucia could not be reached securely. The protected connection remains saved. Check the network, HTTPS address and certificate trust, then retry.",
            CompanionConnectionException => "Lucia could not complete the secure connection. Check the HTTPS host and certificate trust. Do not replay a code whose delivery may have completed; request a new one.",
            SessionStateException => "The saved connection could not be recovered safely. Review or remove it before pairing again.",
            OperationCanceledException => "The operation stopped. Review the saved connection before starting again.",
            _ => "The connection could not be completed. Retry or review protected storage before pairing again."
        };
    }
    private void Notify() => Changed?.Invoke();
}
