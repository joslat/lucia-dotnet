namespace lucia.Agents.Companion;

public interface ICompanionSessionService
{
    /// <summary>Trusted owner-approval code only. Never map directly to anonymous/native issuance.</summary>
    Task<CompanionSessionTokens> CreateAsync(CompanionGrant grant, string deviceName, CancellationToken cancellationToken = default);
    Task<CompanionSession?> AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<CompanionSessionTokens?> RenewAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string sessionId, string homeId, CancellationToken cancellationToken = default);
    Task RevokeWithRefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<int> PruneExpiredAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompanionSession>> ListActiveAsync(string homeId, CancellationToken cancellationToken = default);
}
