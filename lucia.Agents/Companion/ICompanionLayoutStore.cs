namespace lucia.Agents.Companion;

public interface ICompanionLayoutStore
{
    Task<CompanionLayoutSnapshot?> ReadAsync(string homeId, CancellationToken cancellationToken = default);
    /// <summary>Null expected revision means create only. Null result means a conflict, never an overwrite.</summary>
    Task<CompanionLayoutSnapshot?> TrySaveAsync(CompanionLayout layout, string? expectedRevision, string ownerId,
        CancellationToken cancellationToken = default);
}
