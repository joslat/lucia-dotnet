using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Screens;

/// <summary>Shows local metadata alternatives. The session owns revision checks and recovery.</summary>
public sealed partial class HomeSaveConflictScreen : UserControl
{
    public HomeSaveConflictScreen() { AvaloniaXamlLoader.Load(this); }

    public void Refresh(HomeProfile current, HomeProfileSession session)
    {
        this.FindControl<TextBlock>("SessionSummary")!.Text = Summary(current);
        this.FindControl<TextBlock>("SessionDetails")!.Text = Details(current);
        this.FindControl<TextBlock>("ReviewStatus")!.Text = session.StatusText;
        var saved = session.ConflictSnapshot;
        this.FindControl<TextBlock>("SavedSummary")!.Text = saved is null ? "No saved version available for review. Read it again before choosing."
            : saved.Profile is { } profile ? Summary(profile) + (profile.HomeId != current.HomeId
                ? "\nThis file belongs to another home and cannot be loaded into this session." : string.Empty)
            : saved.IsUnreadable ? "The saved file cannot be read as a supported home setup. It will stay untouched unless you explicitly replace it."
            : "The saved file has been removed. Keeping your session changes creates it again.";
        this.FindControl<TextBlock>("SavedDetails")!.Text = saved?.Profile is { } details ? Details(details) : string.Empty;
        this.FindControl<Expander>("SavedDetailsExpander")!.IsVisible = saved?.Profile is not null;
        this.FindControl<Button>("KeepSessionChanges")!.IsEnabled = saved is not null && !session.IsLoading;
        this.FindControl<Button>("UseSavedSetup")!.IsEnabled = session.CanUseSavedSetup && !session.IsLoading;
        this.FindControl<Button>("ReadSavedAgain")!.IsEnabled = !session.IsLoading;
    }

    private static string Summary(HomeProfile profile)
    {
        var pins = profile.Places.Where(place => profile.PinnedPlaceIds.Contains(place.Id)).Select(place => place.Name);
        return $"{profile.HomeName}\n{profile.Places.Count} places, {profile.Connections.Count} connections, {profile.Bindings?.Count ?? 0} bindings\n"
            + $"Home sections: {string.Join(" → ", (profile.SectionOrder ?? HomeSections.DefaultOrder).Select(HomeSections.Label))}\n"
            + $"Pinned places: {(profile.PinnedPlaceIds.Count == 0 ? "None" : string.Join(", ", pins))}";
    }

    private static string Details(HomeProfile profile) => string.Join("\n\n", profile.Places.Select(place =>
    {
        var neighbors = profile.Connections.Where(edge => edge.FromPlaceId == place.Id || edge.ToPlaceId == place.Id)
            .Select(edge => edge.FromPlaceId == place.Id ? edge.ToPlaceId : edge.FromPlaceId)
            .Select(id => profile.Places.Single(candidate => candidate.Id == id).Name).ToArray();
        var bindings = (profile.Bindings ?? []).Where(binding => binding.PlaceId == place.Id)
            .Select(binding => $"{binding.Role}: {binding.SourceId}").ToArray();
        return $"{place.Name} [{place.Id}]\nIcon: {place.Icon}\nConnects to: {(neighbors.Length == 0 ? "None" : string.Join(", ", neighbors))}\n"
            + $"Bindings: {(bindings.Length == 0 ? "None" : string.Join("; ", bindings))}";
    }));
}
