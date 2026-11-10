using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>The semantic brush resource key for each place state, shared by the place page and the side pane.</summary>
public static class PlaceStateBrushes
{
    public static string Key(PlaceState state) => state switch
    {
        PlaceState.Problem => "ProblemBrush",
        PlaceState.Waiting or PlaceState.Changed => "AccentBrush",
        PlaceState.Working => "InfoBrush",
        _ => "MutedBrush",
    };

    /// <summary>The short status shown in the page's chip and the side pane: the attention kind, or the Home status line.</summary>
    public static string StatusLabel(Place place) => place.State switch
    {
        PlaceState.Problem or PlaceState.Waiting or PlaceState.Unavailable => HomeAttention.KindLabel(place.State),
        PlaceState.Quiet => "Quiet",
        _ => PlaceCaption.Status(place) ?? place.State.ToString(),
    };
}
