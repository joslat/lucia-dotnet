namespace lucia.Agents.Companion;

public interface ICompanionPairingService
{
    Task<CompanionPairingChallenge?> StartAsync(string deviceName, CancellationToken cancellationToken = default);
    /// <summary>Owner-only lookup; neither device secret nor credentials are exposed.</summary>
    Task<CompanionPairingReview?> ReviewAsync(string userCode, CancellationToken cancellationToken = default);
    Task<bool> ApproveAsync(string pairingId, CompanionGrant grant, string approverId, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(string pairingId, CancellationToken cancellationToken = default);
    Task<CompanionPairingResult> RedeemAsync(string deviceCode, CancellationToken cancellationToken = default);
    Task<int> PruneExpiredAsync(CancellationToken cancellationToken = default);
}
