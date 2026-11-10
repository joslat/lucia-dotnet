using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>
/// Named native shortcuts in a radial composition, or a list at large text sizes. Every action is a button; holding a place
/// and sliding to an action is only a faster way to reach the same buttons.
/// </summary>
public sealed partial class BloomActions : UserControl
{
    private static readonly (BloomAction Action, string Radial, string List)[] Buttons =
    [
        (BloomAction.Open, "OpenButton", "LargeOpen"),
        (BloomAction.History, "HistoryButton", "LargeHistory"),
        (BloomAction.Pin, "PinButton", "LargePin"),
        (BloomAction.Close, "CloseButton", "LargeClose"),
    ];

    public event Action<BloomAction>? ActionRequested;

    public BloomActions()
    {
        AvaloniaXamlLoader.Load(this);
        foreach (var (action, radial, list) in Buttons)
        {
            this.FindControl<Button>(radial)!.Click += (_, _) => ActionRequested?.Invoke(action);
            this.FindControl<Button>(list)!.Click += (_, _) => ActionRequested?.Invoke(action);
        }
        PropertyChanged += (_, change) => { if (change.Property == FontSizeProperty) { AdaptText(); } };
        AttachedToVisualTree += (_, _) => AdaptText();
        KeyDown += (_, args) => { if (args.Key == Key.Escape) { ActionRequested?.Invoke(BloomAction.Close); args.Handled = true; } };
    }

    public bool IsLargeText => FontSize > 20;

    public void SetPlace(Place place, bool pinned)
    {
        this.FindControl<TextBlock>("Heading")!.Text = place.Name;
        this.FindControl<TextBlock>("LargeHeading")!.Text = place.Name;
        this.FindControl<Avalonia.Controls.Shapes.Path>("PlaceIcon")!.Data = PlaceIcons.For(place.Icon);
        this.FindControl<BloomActionButton>("PinButton")!.Label = pinned ? "Unpin" : "Pin place";
        this.FindControl<Button>("LargePin")!.Content = pinned ? "Unpin" : "Pin place";
        foreach (var (action, radial, list) in Buttons)
        {
            var name = action switch
            {
                BloomAction.Open => $"Open {place.Name}",
                BloomAction.History => $"{place.Name} history in Story",
                BloomAction.Pin => $"{(pinned ? "Unpin" : "Pin")} {place.Name}",
                _ => $"Close {place.Name} actions",
            };
            this.FindControl<Button>(radial)!.SetValue(AutomationProperties.NameProperty, name);
            this.FindControl<Button>(list)!.SetValue(AutomationProperties.NameProperty, name);
        }
    }

    /// <summary>The action whose button is under a point in this control's coordinates, if any.</summary>
    public BloomAction? ActionAt(Point point)
    {
        foreach (var (action, radial, list) in Buttons)
        {
            var button = this.FindControl<Button>(IsLargeText ? list : radial)!;
            if (button.IsEffectivelyVisible && button.TranslatePoint(default, this) is { } origin && new Rect(origin, button.Bounds.Size).Contains(point))
            {
                return action;
            }
        }
        return null;
    }

    /// <summary>True when a point has left the Bloom, which cancels a slide.</summary>
    public bool IsOutside(Point point) => !new Rect(Bounds.Size).Contains(point);

    /// <summary>Highlights the action a slide would choose on release. Null clears it.</summary>
    public void Arm(BloomAction? armed)
    {
        foreach (var (action, radial, list) in Buttons)
        {
            this.FindControl<Button>(radial)!.Classes.Set("armed", action == armed);
            this.FindControl<Button>(list)!.Classes.Set("armed", action == armed);
        }
    }

    private void AdaptText()
    {
        this.FindControl<Grid>("RadialLayout")!.IsVisible = !IsLargeText;
        var fallback = this.FindControl<Border>("LargeTextActions")!;
        fallback.IsVisible = IsLargeText;
        fallback.MaxHeight = Math.Max(120, (TopLevel.GetTopLevel(this)?.Bounds.Height ?? 464) - 64);
    }

    public void FocusFirstAction() => this.FindControl<Button>(IsLargeText ? "LargeOpen" : "OpenButton")!.Focus();
}
