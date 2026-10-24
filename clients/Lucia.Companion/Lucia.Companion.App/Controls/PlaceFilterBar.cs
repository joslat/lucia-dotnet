using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Lucia.Companion.App.ViewModels;

namespace Lucia.Companion.App.Controls;

/// <summary>A manually scrollable strip plus a native chooser. No automatically rotating carousel.</summary>
public sealed class PlaceFilterBar : UserControl
{
    private StoryViewModel? state;
    private readonly ComboBox chooser = new() { MinHeight = 48, MaxDropDownHeight = 320, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel strip = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private bool updating;

    public PlaceFilterBar()
    {
        chooser.SetValue(AutomationProperties.NameProperty, "Filter story by place");
        chooser.SelectionChanged += (_, _) =>
        {
            if (!updating && state is not null)
            {
                state.SelectPlace(chooser.SelectedIndex <= 0 ? null : state.Day.Places[chooser.SelectedIndex - 1].Id);
            }
        };
        Content = new StackPanel { Spacing = 8, Children =
        {
            new ScrollViewer { Content = strip, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
            chooser
        }};
    }

    public void SetState(StoryViewModel value)
    {
        updating = true;
        if (!ReferenceEquals(state, value))
        {
            state = value;
            chooser.ItemsSource = new[] { "All places" }.Concat(value.Day.Places.Select(place => place.Name)).ToArray();
            strip.Children.Clear();
            AddChip(null, "All places");
            foreach (var place in value.Day.Places) { AddChip(place.Id, place.Name); }
        }
        chooser.IsVisible = value.Day.Places.Count > 6;
        chooser.SelectedIndex = state.SelectedPlaceId is null ? 0 : state.Day.Places.ToList().FindIndex(place => place.Id == state.SelectedPlaceId) + 1;
        updating = false;
        foreach (var button in strip.Children.OfType<Button>())
        {
            var selected = Equals(button.Tag, state.SelectedPlaceId);
            button[!Button.BorderBrushProperty] = new DynamicResourceExtension(selected ? "AccentBrush" : "GraphLineBrush");
            button.BorderThickness = new Thickness(selected ? 2 : 1);
            button.SetValue(AutomationProperties.HelpTextProperty, selected ? "Selected place filter." : "Filter story by this place.");
        }
    }

    private void AddChip(string? id, string name)
    {
        var button = new Button { Content = name, Tag = id, Classes = { "theme" }, CornerRadius = new CornerRadius(24) };
        button.Click += (_, _) => state!.SelectPlace(id);
        strip.Children.Add(button);
    }
}
