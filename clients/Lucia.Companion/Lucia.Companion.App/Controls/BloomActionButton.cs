using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lucia.Companion.App.Controls;

/// <summary>A named circular action, distinct from a place or a status indicator.</summary>
public sealed class BloomActionButton : Button
{
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<BloomActionButton, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<BloomActionButton, Geometry?>(nameof(Icon));
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
}
