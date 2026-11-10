using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>
/// "Needs you": one row per place that needs attention, most urgent first. Each row names the place and the kind of
/// attention, gives the reason and the age of the place's latest reading, and opens the place's detail.
/// </summary>
public sealed class AttentionList : UserControl
{
    private readonly List<Button> rows = [];
    public event Action<string>? PlaceInvoked;
    public string Heading { get; private set; } = string.Empty;
    public IReadOnlyList<Button> Rows => rows;

    public void SetPlaces(IReadOnlyList<Place> places, bool isDemo = true, DateTimeOffset? reportTime = null)
    {
        ArgumentNullException.ThrowIfNull(places);
        rows.Clear();
        Heading = places.Count > 0 ? "Needs you" : isDemo ? "Nothing needs you in this sample" : "No configured reports flagged";
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock { Text = Heading, Classes = { "section" }, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) });
        for (var index = 0; index < places.Count; index++)
        {
            var row = Row(places[index], reportTime, lead: index == 0);
            rows.Add(row);
            stack.Children.Add(row);
        }
        Content = stack;
    }

    private Button Row(Place place, DateTimeOffset? reportTime, bool lead)
    {
        var kind = HomeAttention.KindLabel(place.State);
        var reading = HomeAttention.ReadingLine(place, reportTime);
        var (stateClass, brush) = place.State switch
        {
            PlaceState.Problem => ("problem", "ProblemBrush"),
            PlaceState.Waiting => ("waiting", "AccentBrush"),
            _ => ("unavailable", "MutedBrush"),
        };

        var tint = new Ellipse { Opacity = place.State == PlaceState.Unavailable ? 0 : 0.14, [!Shape.FillProperty] = new DynamicResourceExtension(brush) };
        var ring = new Ellipse { StrokeThickness = 1, [!Shape.StrokeProperty] = new DynamicResourceExtension(brush) };
        if (place.State == PlaceState.Unavailable) { ring.StrokeDashArray = [2, 3]; }
        var icon = new Grid { Width = 36, Height = 36, VerticalAlignment = VerticalAlignment.Top };
        icon.Children.Add(tint);
        icon.Children.Add(ring);
        icon.Children.Add(new Viewbox
        {
            Width = 20, Height = 20,
            Child = new Path
            {
                Data = PlaceStateIcons.For(place.State), Width = 24, Height = 24, Stretch = Stretch.None, StrokeThickness = 1.7,
                StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, [!Shape.StrokeProperty] = new DynamicResourceExtension(brush),
            },
        });

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = $"{place.Name} · {kind}", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap,
            [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13"), [!TextBlock.ForegroundProperty] = new DynamicResourceExtension(brush),
        });
        text.Children.Add(new TextBlock
        {
            Text = place.Reason, TextWrapping = TextWrapping.Wrap, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis,
            FontWeight = lead ? FontWeight.SemiBold : FontWeight.Normal,
        });
        text.Children.Add(new TextBlock { Text = reading, Classes = { "muted" }, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13") });

        var chevron = new Path
        {
            Data = AttentionIcons.Chevron, Width = 24, Height = 24, Stretch = Stretch.None, StrokeThickness = 1.7, VerticalAlignment = VerticalAlignment.Center,
            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, [!Shape.StrokeProperty] = new DynamicResourceExtension("MutedBrush"),
        };
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        layout.Children.Add(icon);
        Grid.SetColumn(text, 1);
        layout.Children.Add(text);
        Grid.SetColumn(chevron, 2);
        layout.Children.Add(chevron);

        var button = new Button { Content = layout, Tag = place.Id, Classes = { stateClass }, [!ThemeProperty] = new DynamicResourceExtension("AttentionRowButton") };
        if (lead) { button.Classes.Add("lead"); }
        var reason = place.Reason.TrimEnd();
        if (!reason.EndsWith('.')) { reason += "."; }
        button.SetValue(AutomationProperties.NameProperty, $"{place.Name}. {kind}. {reason} {reading}. Show detail.");
        button.Click += (_, _) => PlaceInvoked?.Invoke(place.Id);
        return button;
    }
}
