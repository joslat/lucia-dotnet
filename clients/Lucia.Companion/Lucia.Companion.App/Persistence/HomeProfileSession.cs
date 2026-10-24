using System.Text.Json;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Persistence;

/// <summary>Coordinates local setup I/O without putting filesystem state or recovery policy into controls.</summary>
public sealed class HomeProfileSession
{
    private readonly HomeViewModel state;
    private readonly LocalHomeStore? store;
    private Task? initialization;
    private string? savedRevision;
    private bool hasBaseline;
    private long saveRequest;
    public event Action? Changed;
    public bool IsLoading { get; private set; }
    public bool HasUnreadableProfile { get; private set; }
    public bool CanReplaceUnreadableProfile => HasUnreadableProfile && hasBaseline;
    public bool HasConflict { get; private set; }
    public LocalHomeSnapshot? ConflictSnapshot { get; private set; }
    public bool CanUseSavedSetup => ConflictSnapshot?.Profile?.HomeId == state.Home.Id;
    public bool CanRetry { get; private set; }
    public string StatusText { get; private set; } = "Changes last for this session.";
    public Task PendingSave { get; private set; } = Task.CompletedTask;

    public HomeProfileSession(HomeViewModel state, LocalHomeStore? store)
    {
        this.state = state;
        this.store = store;
        state.PreferencesChanged += QueueSave;
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => initialization ??= LoadAsync(cancellationToken);

    public void ApplySetup(HomeTopology home, bool replaceUnreadableProfile = false)
    {
        if (HasUnreadableProfile && (!replaceUnreadableProfile || !hasBaseline))
        {
            throw new InvalidOperationException("The saved file must be read before explicit replacement is safe.");
        }

        if (home.Id != state.Home.Id) { throw new ArgumentException("The setup belongs to another home.", nameof(home)); }
        HasUnreadableProfile = false;
        state.ReplaceHome(home);
    }

    public void RetrySave() => QueueSave();

    public async Task ReviewConflictAsync()
    {
        if (store is null || !HasConflict || IsLoading) { return; }
        IsLoading = true;
        SetStatus("Reading the saved setup for review...");
        try
        {
            ConflictSnapshot = await store.LoadSnapshotAsync();
            SetStatus("Review both versions. Nothing changes until you choose.");
        }
        catch (Exception error) when (IsStorageError(error))
        {
            ConflictSnapshot = null;
            SetStatus("Could not read the saved setup. Your session changes are untouched. Try reading it again.");
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    public async Task<bool> ResolveConflictAsync(bool keepSessionChanges)
    {
        if (store is null || !HasConflict || IsLoading || ConflictSnapshot is not { } reviewed) { return false; }
        if (!keepSessionChanges && !CanUseSavedSetup) { return false; }
        IsLoading = true;
        SetStatus("Checking the reviewed setup...");
        try
        {
            if (keepSessionChanges) { savedRevision = await store.SaveAsync(CurrentProfile(), reviewed.Revision); }
            else
            {
                var current = await store.LoadSnapshotAsync();
                if (current.Revision != reviewed.Revision) { throw new HomeSaveConflictException(); }
                Restore(reviewed.Profile!);
                savedRevision = reviewed.Revision;
            }
            hasBaseline = true;
            HasConflict = false;
            HasUnreadableProfile = false;
            CanRetry = false;
            ConflictSnapshot = null;
            SetStatus(keepSessionChanges ? "Your changes are saved on this device." : "Using the saved setup. Previous session changes were discarded.");
            return true;
        }
        catch (HomeSaveConflictException)
        {
            ConflictSnapshot = null;
            IsLoading = false;
            await ReviewConflictAsync();
            SetStatus("The saved setup changed again. Your session is untouched. Review the latest version before choosing.");
            return false;
        }
        catch (Exception error) when (IsStorageError(error))
        {
            SetStatus("Could not finish that choice. Your session changes are untouched. Try again when storage is available.");
            return false;
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (store is null) { return; }
        IsLoading = true;
        SetStatus("Loading local setup...");
        try
        {
            var snapshot = await store.LoadSnapshotAsync(cancellationToken);
            savedRevision = snapshot.Revision;
            hasBaseline = true;
            if (snapshot.IsUnreadable) { throw new JsonException("Unreadable setup."); }
            if (snapshot.Profile is not null) { Restore(snapshot.Profile); }

            SetStatus("Local setup. Changes save on this device.");
        }
        catch (Exception error) when (IsStorageError(error))
        {
            HasUnreadableProfile = true;
            SetStatus(hasBaseline
                ? "Saved setup could not be read. The original file is untouched. Build your home to review and replace it; automatic saving is off."
                : "Saved setup could not be accessed safely. The file is untouched and saving is off. Reopen the app when storage is available.");
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    private void Restore(HomeProfile profile)
    {
        var restored = profile.ApplyTo(state.Home);
        state.ReplaceHome(restored, notifyPreferences: false);
        state.RestorePins(profile.PinnedPlaceIds);
        state.SetSectionOrder(profile.SectionOrder ?? HomeSections.DefaultOrder, notifyPreferences: false);
    }

    private HomeProfile CurrentProfile() => HomeProfile.From(state.Home, state.PinnedPlaceIds, state.SectionOrder);

    private void QueueSave()
    {
        if (store is null || !hasBaseline || HasUnreadableProfile || HasConflict || IsLoading) { return; }
        var request = ++saveRequest;
        PendingSave = SaveAfterAsync(PendingSave, CurrentProfile(), request);
    }

    private async Task SaveAfterAsync(Task previous, HomeProfile snapshot, long request)
    {
        await previous;
        if (HasConflict) { return; }
        SetStatus("Saving local setup...");
        try
        {
            savedRevision = await store!.SaveAsync(snapshot, savedRevision);
            if (request != saveRequest) { return; }
            CanRetry = false;
            SetStatus("Setup saved on this device. No readings or task history are stored.");
        }
        catch (HomeSaveConflictException)
        {
            HasConflict = true;
            CanRetry = false;
            ConflictSnapshot = null;
            SetStatus("Saved setup changed elsewhere. Your changes remain in this session. Review the save conflict to continue saving.");
        }
        catch (Exception error) when (IsStorageError(error))
        {
            if (request != saveRequest) { return; }
            CanRetry = true;
            SetStatus("Could not save on this device. Your changes still work in this session. Retry when storage is available.");
        }
    }

    private void SetStatus(string text) { StatusText = text; Changed?.Invoke(); }
    private static bool IsStorageError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException;
}
