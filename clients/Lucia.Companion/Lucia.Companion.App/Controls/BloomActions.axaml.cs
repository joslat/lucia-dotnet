using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Named native shortcuts in a radial composition. No hover, hold or gesture is required.</summary>
public sealed partial class BloomActions : UserControl
{
    public event Action? ReadingsRequested;
    public event Action? PinRequested;
    public event Action? CloseRequested;

    public BloomActions()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Button>("ReadingsButton")!.Click += (_, _) => ReadingsRequested?.Invoke();
        this.FindControl<Button>("PinButton")!.Click += (_, _) => PinRequested?.Invoke();
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => CloseRequested?.Invoke();
        this.FindControl<Button>("LargeReadings")!.Click += (_, _) => ReadingsRequested?.Invoke();
        this.FindControl<Button>("LargePin")!.Click += (_, _) => PinRequested?.Invoke();
        this.FindControl<Button>("LargeClose")!.Click += (_, _) => CloseRequested?.Invoke();
        PropertyChanged += (_, change) => { if (change.Property == FontSizeProperty) { AdaptText(); } };
        AttachedToVisualTree += (_, _) => AdaptText();
        KeyDown += (_, args) => { if (args.Key == Key.Escape) { CloseRequested?.Invoke(); args.Handled = true; } };
    }

    public void SetPlace(Place place, bool pinned)
    {
        this.FindControl<TextBlock>("Heading")!.Text = place.Name;
        this.FindControl<TextBlock>("LargeHeading")!.Text = place.Name;
        this.FindControl<Avalonia.Controls.Shapes.Path>("PlaceIcon")!.Data = PlaceIcons.For(place.Icon);
        this.FindControl<BloomActionButton>("PinButton")!.Label = pinned ? "Unpin" : "Pin place";
        this.FindControl<Button>("LargePin")!.Content = pinned ? "Unpin" : "Pin place";
        this.FindControl<Button>("ReadingsButton")!.SetValue(AutomationProperties.NameProperty, $"View {place.Name} readings");
        this.FindControl<Button>("PinButton")!.SetValue(AutomationProperties.NameProperty, $"{(pinned ? "Unpin" : "Pin")} {place.Name}");
        this.FindControl<Button>("CloseButton")!.SetValue(AutomationProperties.NameProperty, $"Close {place.Name} actions");
    }

    private void AdaptText()
    {
        var large = FontSize > 20;
        this.FindControl<Grid>("RadialLayout")!.IsVisible = !large;
        var fallback = this.FindControl<Border>("LargeTextActions")!;
        fallback.IsVisible = large;
        fallback.MaxHeight = Math.Max(120, (TopLevel.GetTopLevel(this)?.Bounds.Height ?? 464) - 64);
    }

    public void FocusFirstAction() => this.FindControl<Button>(FontSize > 20 ? "LargeReadings" : "ReadingsButton")!.Focus();
}
