using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Threading;
using Lucia.Companion.App.ViewModels;

namespace Lucia.Companion.App.Controls;

/// <summary>A manually scrollable strip plus a native chooser. No automatically rotating carousel.</summary>
public sealed class PlaceFilterBar : UserControl
{
    private StoryViewModel? state;
    private readonly ComboBox chooser = new() { MinHeight = 48, MaxDropDownHeight = 320, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel strip = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly ScrollViewer scroll;
    private bool updating;
    private string? renderedPlace;
    private int revealVersion;

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
        scroll = new ScrollViewer { Content = strip, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Content = new StackPanel { Spacing = 8, Children =
        {
            scroll,
            chooser
        }};
        AttachedToVisualTree += (_, _) => RevealSelected();
        DetachedFromVisualTree += (_, _) => revealVersion++;
        scroll.SizeChanged += (_, _) => RevealSelected();
        strip.SizeChanged += (_, _) => RevealSelected();
        _ = new PresentationVisibility(this, () => { revealVersion++; if (IsEffectivelyVisible) { RevealSelected(); } });
    }

    public void SetState(StoryViewModel value)
    {
        updating = true;
        var changed = !ReferenceEquals(state, value) || renderedPlace != value.SelectedPlaceId;
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
        renderedPlace = value.SelectedPlaceId;
        if (changed) { RevealSelected(); }
    }

    private void RevealSelected()
    {
        var version = ++revealVersion;
        var model = state;
        var place = model?.SelectedPlaceId;
        var root = TopLevel.GetTopLevel(this);
        Dispatcher.UIThread.Post(() =>
        {
            if (version != revealVersion || model is null || !ReferenceEquals(state, model) || model.SelectedPlaceId != place
                || !IsEffectivelyVisible || root is null || !ReferenceEquals(root, TopLevel.GetTopLevel(this)) || scroll.Viewport.Width <= 0) { return; }
            var chip = strip.Children.OfType<Button>().SingleOrDefault(button => Equals(button.Tag, place));
            if (chip?.TranslatePoint(default, strip) is not { } point) { return; }
            var offset = scroll.Offset.X;
            if (point.X < offset || chip.Bounds.Width > scroll.Viewport.Width) { offset = point.X; }
            else if (point.X + chip.Bounds.Width > offset + scroll.Viewport.Width) { offset = point.X + chip.Bounds.Width - scroll.Viewport.Width; }
            // Bringing the chip into view would also move Story's vertical reading position.
            scroll.Offset = new Vector(Math.Clamp(offset, 0, Math.Max(0, scroll.Extent.Width - scroll.Viewport.Width)), scroll.Offset.Y);
        }, DispatcherPriority.Loaded);
    }

    private void AddChip(string? id, string name)
    {
        var button = new Button { Content = name, Tag = id, Classes = { "theme" }, CornerRadius = new CornerRadius(24) };
        button.Click += (_, _) => state!.SelectPlace(id);
        strip.Children.Add(button);
    }
}
