using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Lucia.Companion.Core;
using Path = Avalonia.Controls.Shapes.Path;

namespace Lucia.Companion.App.Controls;

/// <summary>A stable attention action whose current words and semantics update immediately.</summary>
internal sealed class AttentionPlaceRow : Button
{
    private readonly Ellipse tint = new();
    private readonly Ellipse ring = new() { StrokeThickness = 1 };
    private readonly Path stateIcon = new() { Width = 24, Height = 24, Stretch = Stretch.None, StrokeThickness = 1.7, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round };
    private readonly TextBlock kindText = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13") };
    private readonly TextBlock reasonText = new() { TextWrapping = TextWrapping.Wrap, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock readingText = new() { Classes = { "muted" }, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13") };

    public AttentionPlaceRow(Action<string> invoked)
    {
        this[!ThemeProperty] = new DynamicResourceExtension("AttentionRowButton");
        var icon = new Grid { Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Top, Children = { tint, ring, new Viewbox { Width = 20, Height = 20, Child = stateIcon } } };
        var text = new StackPanel { Spacing = 2, Children = { kindText, reasonText, readingText } };
        var chevron = new Path { Data = AttentionIcons.Chevron, Width = 24, Height = 24, Stretch = Stretch.None, StrokeThickness = 1.7, VerticalAlignment = VerticalAlignment.Center, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, [!Shape.StrokeProperty] = new DynamicResourceExtension("MutedBrush") };
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        layout.Children.Add(icon); Grid.SetColumn(text, 1); layout.Children.Add(text); Grid.SetColumn(chevron, 2); layout.Children.Add(chevron);
        Content = layout;
        Click += (_, _) => invoked((string)Tag!);
    }

    public void Update(Place place, DateTimeOffset? reportTime, bool lead)
    {
        Tag = place.Id;
        var kind = HomeAttention.KindLabel(place.State);
        var reading = HomeAttention.ReadingLine(place, reportTime);
        var brush = place.State switch { PlaceState.Problem => "ProblemBrush", PlaceState.Waiting => "AccentBrush", _ => "MutedBrush" };
        Classes.Set("problem", place.State == PlaceState.Problem);
        Classes.Set("waiting", place.State == PlaceState.Waiting);
        Classes.Set("unavailable", place.State is not (PlaceState.Problem or PlaceState.Waiting));
        Classes.Set("lead", lead);
        tint.Opacity = place.State == PlaceState.Unavailable ? 0 : .14;
        tint[!Shape.FillProperty] = new DynamicResourceExtension(brush);
        ring[!Shape.StrokeProperty] = new DynamicResourceExtension(brush);
        ring.StrokeDashArray = place.State == PlaceState.Unavailable ? [2, 3] : null;
        stateIcon.Data = PlaceStateIcons.For(place.State);
        stateIcon[!Shape.StrokeProperty] = new DynamicResourceExtension(brush);
        kindText.Text = $"{place.Name} · {kind}";
        kindText[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(brush);
        reasonText.Text = place.Reason;
        reasonText.FontWeight = lead ? FontWeight.SemiBold : FontWeight.Normal;
        readingText.Text = reading;
        var reason = place.Reason.TrimEnd();
        if (!reason.EndsWith('.')) { reason += "."; }
        AutomationProperties.SetName(this, $"{place.Name}. {kind}. {reason} {reading}. Show detail.");
    }
}
