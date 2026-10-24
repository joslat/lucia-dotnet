namespace Lucia.Companion.Connection;

public interface ISessionVault
{
    Task<ISessionVaultLease> OpenAsync(CancellationToken cancellationToken = default);
}
