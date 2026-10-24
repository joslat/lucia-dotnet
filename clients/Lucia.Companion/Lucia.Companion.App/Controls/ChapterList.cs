using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Lucia.Companion.App.ViewModels;

namespace Lucia.Companion.App.Controls;

/// <summary>Named evidence-linked native buttons are the accessible alternative to dial hit targets.</summary>
public sealed class ChapterList : UserControl
{
    private string renderedKey = "";
    private readonly StackPanel chapters = new() { Spacing = 8 };
    public ChapterList() => Content = chapters;

    public void SetState(StoryViewModel state)
    {
        var key = state.SelectedPlaceId + ":" + string.Join("|", state.VisibleChapters.Select(chapter => chapter.Id));
        if (renderedKey != key || chapters.Children.Count == 0)
        {
            renderedKey = key;
            chapters.Children.Clear();
            if (state.VisibleChapters.Count == 0)
            {
                chapters.Children.Add(new TextBlock { Text = "No recorded chapters for this place. Missing reports do not mean nothing happened.", Classes = { "muted" } });
            }
            foreach (var chapter in state.VisibleChapters)
            {
                var content = new StackPanel { Spacing = 4 };
                content.Children.Add(new TextBlock { Text = chapter.Title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
                content.Children.Add(new TextBlock { Text = StoryDrawing.RangeLabel(state.Day, chapter.Start, chapter.End), Classes = { "muted" }, FontSize = 13 });
                var evidence = state.Day.EvidenceFor(chapter.Id).Where(interval => state.SelectedPlaceId is null || interval.PlaceId == state.SelectedPlaceId).ToArray();
                content.Children.Add(new TextBlock { Text = string.Join(" · ", evidence.Select(interval => interval.Label)),
                    Classes = { "muted" }, FontSize = 13, TextWrapping = TextWrapping.Wrap });
                var leadingPlace = state.Day.Places.Single(place => place.Id == evidence[0].PlaceId);
                var glyph = new Avalonia.Controls.Shapes.Path { Data = PlaceIcons.For(leadingPlace.Icon),
                    Width = 16, Height = 16, Stretch = Stretch.Uniform, StrokeThickness = 1.7 };
                glyph[!Avalonia.Controls.Shapes.Path.StrokeProperty] = new DynamicResourceExtension("MutedBrush");
                var circle = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(1),
                    Child = glyph, Padding = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Left };
                circle[!Border.BorderBrushProperty] = new DynamicResourceExtension("GraphLineBrush");
                circle[!Border.BackgroundProperty] = new DynamicResourceExtension("ControlBrush");
                var rail = new StackPanel { Spacing = 5 };
                rail.Children.Add(circle);
                rail.Children.Add(new TextBlock { Text = state.Day.TimeLabel(chapter.Start)[..5], FontSize = 12, Classes = { "muted" } });
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("52,*"), ColumnSpacing = 8 };
                row.Children.Add(rail);
                Grid.SetColumn(content, 1);
                row.Children.Add(content);
                var button = new Button { Content = row, Tag = chapter.Id, Classes = { "theme" }, Padding = new Thickness(12),
                    HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                button.SetValue(AutomationProperties.NameProperty, $"{chapter.Title}. {StoryDrawing.RangeLabel(state.Day, chapter.Start, chapter.End)}. Select chapter.");
                button.Click += (_, _) => state.SelectChapter(chapter.Id);
                chapters.Children.Add(button);
            }
        }
        foreach (var button in chapters.Children.OfType<Button>())
        {
            var selected = Equals(button.Tag, state.SelectedChapterId);
            button[!Button.BorderBrushProperty] = new DynamicResourceExtension(selected ? "AccentBrush" : "GraphLineBrush");
            button.BorderThickness = new Thickness(selected ? 2 : 0);
            button.SetValue(AutomationProperties.HelpTextProperty, selected ? "Selected chapter. Orbit and timelines show its start." : "Select this chapter in the orbit and timelines.");
        }
    }
}
