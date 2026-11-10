using Avalonia.Media;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>The one icon per place state, shared by orbs, the place page chip and Needs you. Quiet and Changed have none.</summary>
public static class PlaceStateIcons
{
    public static Geometry? For(PlaceState state) => state switch
    {
        PlaceState.Problem => AttentionIcons.Fault,
        PlaceState.Waiting => AttentionIcons.Choice,
        PlaceState.Unavailable => AttentionIcons.Silent,
        PlaceState.Working => NavigationIcons.Lucia,
        _ => null,
    };
}
