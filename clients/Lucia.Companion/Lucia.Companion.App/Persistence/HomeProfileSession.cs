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
    private long saveRevision;
    public event Action? Changed;
    public bool IsLoading { get; private set; }
    public bool HasUnreadableProfile { get; private set; }
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
        if (HasUnreadableProfile && !replaceUnreadableProfile)
        {
            throw new InvalidOperationException("Explicit replacement is required to preserve the unreadable setup.");
        }

        if (home.Id != state.Home.Id) { throw new ArgumentException("The setup belongs to another home.", nameof(home)); }
        HasUnreadableProfile = false;
        state.ReplaceHome(home);
    }

    public void RetrySave() => QueueSave();

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (store is null) { return; }
        IsLoading = true;
        SetStatus("Loading local setup...");
        try
        {
            var profile = await store.LoadAsync(cancellationToken);
            if (profile is not null)
            {
                var restored = profile.ApplyTo(state.Home);
                state.ReplaceHome(restored, notifyPreferences: false);
                state.RestorePins(profile.PinnedPlaceIds);
            }

            SetStatus("Local setup. Changes save on this device.");
        }
        catch (Exception error) when (IsStorageError(error))
        {
            HasUnreadableProfile = true;
            SetStatus("Saved setup could not be read. The original file is untouched. Build your home to review and replace it; automatic saving is off.");
        }
        finally { IsLoading = false; Changed?.Invoke(); }
    }

    private void QueueSave()
    {
        if (store is null || HasUnreadableProfile || IsLoading) { return; }
        PendingSave = SaveAsync();
    }

    private async Task SaveAsync()
    {
        var revision = ++saveRevision;
        var snapshot = HomeProfile.From(state.Home, state.PinnedPlaceIds);
        SetStatus("Saving local setup...");
        try
        {
            await store!.SaveAsync(snapshot);
            if (revision != saveRevision) { return; }
            CanRetry = false;
            SetStatus("Setup saved on this device. No readings or task history are stored.");
        }
        catch (Exception error) when (IsStorageError(error))
        {
            if (revision != saveRevision) { return; }
            CanRetry = true;
            SetStatus("Could not save on this device. Your changes still work in this session. Retry when storage is available.");
        }
    }

    private void SetStatus(string text) { StatusText = text; Changed?.Invoke(); }
    private static bool IsStorageError(Exception error) => error is IOException or UnauthorizedAccessException or JsonException or ArgumentException;
}
