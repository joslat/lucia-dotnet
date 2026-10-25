using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Lucia.Companion.Core;
using Path = Avalonia.Controls.Shapes.Path;

namespace Lucia.Companion.App.Controls;

/// <summary>Presentation metadata only. No observation state, network, preference mutation or implicit source ownership.</summary>
public sealed class HomeLayoutSummary : UserControl
{
    public void SetHome(HomeTopology home)
    {
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(new TextBlock { Text = home.Name, Classes = { "section" }, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = $"Places: {home.Places.Count} · Connections: {home.Connections.Count} · Bindings: {home.Bindings.Count}", Classes = { "muted" } });
        var places = new StackPanel { Spacing = 12 };
        foreach (var place in home.Places)
        {
            var row = new Grid { ColumnDefinitions = new("32,*"), ColumnSpacing = 12 };
            var icon = new Path { Data = PlaceIcons.For(place.Icon), Width = 24, Height = 24, Stretch = Stretch.Uniform,
                StrokeThickness = 1.7, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Margin = new(0, 4, 0, 0) };
            icon[!Path.StrokeProperty] = new DynamicResourceExtension("MutedBrush"); row.Children.Add(icon);
            var text = new StackPanel { Spacing = 8 }; Grid.SetColumn(text, 1); row.Children.Add(text);
            text.Children.Add(new TextBlock { Text = place.Name, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            var neighbors = home.Connections.Where(edge => edge.FromPlaceId == place.Id || edge.ToPlaceId == place.Id)
                .Select(edge => edge.FromPlaceId == place.Id ? edge.ToPlaceId : edge.FromPlaceId)
                .Select(id => home.Places.Single(item => item.Id == id).Name);
            text.Children.Add(new TextBlock { Text = $"Icon: {place.Icon} · Connects to: {string.Join(", ", neighbors.DefaultIfEmpty("None configured"))}", Classes = { "muted" } });
            var bindings = home.Bindings.Where(binding => binding.PlaceId == place.Id)
                .Select(binding => $"{binding.Role}: {binding.SourceId}\nSource place: {binding.SourcePlaceId ?? "Unqualified"}");
            text.Children.Add(new Expander
            {
                Header = new TextBlock { Text = "Exact bindings", TextWrapping = TextWrapping.Wrap }, MinHeight = 48,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Content = new TextBlock { Text = $"Place ID: {place.Id}\n" + string.Join("\n", bindings.DefaultIfEmpty("No source bindings")), Classes = { "muted" }, TextWrapping = TextWrapping.Wrap }
            });
            places.Children.Add(row);
        }
        body.Children.Add(places);
        Content = body;
    }
}
