using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

public sealed class AttentionList : UserControl
{
    public event Action<string>? PlaceInvoked;

    public void SetPlaces(IReadOnlyList<Place> places, bool isDemo = true)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock { Text = places.Count == 0 ? isDemo ? "Nothing needs a look in this sample" : "No configured reports flagged" : "Needs a look", Classes = { "section" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        foreach (var place in places)
        {
            var label = new TextBlock { Text = $"{place.Name} · {place.State}", [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize14"), FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            label[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(place.State switch
            {
                PlaceState.Problem => "ProblemBrush", PlaceState.Waiting => "AccentBrush", _ => "MutedBrush"
            });
            var content = new StackPanel { Spacing = 5 };
            content.Children.Add(label);
            content.Children.Add(new TextBlock { Text = place.Reason, Classes = { "muted" }, [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13"), MaxLines = 2, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
            var button = new Button { Content = content, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, Padding = new Thickness(8), Classes = { "theme" } };
            button.SetValue(AutomationProperties.NameProperty, $"{place.Name}. {place.State}. {place.Reason}. Show readings.");
            button.Click += (_, _) => PlaceInvoked?.Invoke(place.Id);
            stack.Children.Add(button);
        }

        Content = stack;
    }
}
