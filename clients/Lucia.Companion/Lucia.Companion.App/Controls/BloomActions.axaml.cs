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
        KeyDown += (_, args) => { if (args.Key == Key.Escape) { CloseRequested?.Invoke(); args.Handled = true; } };
    }

    public void SetPlace(Place place, bool pinned)
    {
        this.FindControl<TextBlock>("Heading")!.Text = place.Name;
        this.FindControl<Avalonia.Controls.Shapes.Path>("PlaceIcon")!.Data = PlaceIcons.For(place.Icon);
        this.FindControl<BloomActionButton>("PinButton")!.Label = pinned ? "Unpin" : "Pin place";
        this.FindControl<Button>("ReadingsButton")!.SetValue(AutomationProperties.NameProperty, $"View {place.Name} readings");
        this.FindControl<Button>("PinButton")!.SetValue(AutomationProperties.NameProperty, $"{(pinned ? "Unpin" : "Pin")} {place.Name}");
        this.FindControl<Button>("CloseButton")!.SetValue(AutomationProperties.NameProperty, $"Close {place.Name} actions");
    }

    public void FocusFirstAction() => this.FindControl<Button>("ReadingsButton")!.Focus();
}
