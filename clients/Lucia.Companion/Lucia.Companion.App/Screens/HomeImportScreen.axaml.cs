using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Lucia.Companion.App.Configuration;
using Lucia.Companion.App.Controls;
using Lucia.Companion.Connection;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Screens;

/// <summary>Review a metadata merge. Applying returns a plan to the parent draft, never a disk write.</summary>
public sealed partial class HomeImportScreen : UserControl
{
    private readonly HomeTopology baseline;
    private readonly HomeCatalog catalog;
    private readonly HomeImportPlaceRow[] rows;
    private HomeImportPlan? reviewed;
    public event Action<HomeImportPlan>? ApplyRequested;
    public event Action? CancelRequested;

    public HomeImportScreen() : this(new HomeTopology("design-preview", "Your home", [], []), new HomeCatalog("design-preview", DateTimeOffset.UtcNow, [])) { }

    public HomeImportScreen(HomeTopology baseline, HomeCatalog catalog)
    {
        if (baseline.Id != catalog.HomeId) { throw new ArgumentException("The catalog belongs to another home."); }
        this.baseline = baseline; this.catalog = catalog;
        AvaloniaXamlLoader.Load(this);
        rows = catalog.Places.Select(place => new HomeImportPlaceRow(place, baseline.Places.FirstOrDefault(existing => existing.Id == place.PlaceId))).ToArray();
        foreach (var row in rows) { row.Changed += Invalidate; this.FindControl<StackPanel>("ImportChoices")!.Children.Add(row); }
        this.FindControl<TextBlock>("ImportEmptyNotice")!.IsVisible = rows.Length == 0;
        this.FindControl<Button>("PreviewImport")!.Click += (_, _) => Preview();
        this.FindControl<Button>("EditImportSelection")!.Click += (_, _) => ReturnToSelection();
        this.FindControl<Button>("AddImportToDraft")!.Click += (_, _) => { if (reviewed is not null) { ApplyRequested?.Invoke(reviewed); } };
        this.FindControl<Button>("CancelImport")!.Click += (_, _) => CancelRequested?.Invoke();
        KeyDown += (_, args) => { if (args.Key == Key.Escape) { RequestBack(); args.Handled = true; } };
        SizeChanged += (_, _) => Adapt();
        this.FindControl<TextBlock>("ImportStatus")!.PropertyChanged += (_, change) => { if (change.Property == TextBlock.FontSizeProperty) { Adapt(); } };
        AttachedToVisualTree += (_, _) => this.FindControl<Button>("CancelImport")!.Focus();
    }

    private void Invalidate()
    {
        reviewed = null;
        var count = rows.Count(row => row.IsSelected);
        this.FindControl<Button>("PreviewImport")!.IsEnabled = count > 0;
        this.FindControl<Button>("AddImportToDraft")!.IsEnabled = false;
        this.FindControl<StackPanel>("ImportReviewChanges")!.Children.Clear();
        this.FindControl<TextBlock>("ImportReviewSummary")!.Text = "Selection changed. Preview again before adding anything.";
        Status(count == 0 ? "Choose at least one place to preview." : $"{count} {(count == 1 ? "place" : "places")} selected. Preview the additions.", false);
        Adapt();
    }

    public void RequestBack()
    {
        if (reviewed is not null && !Expanded) { ReturnToSelection(); }
        else { CancelRequested?.Invoke(); }
    }

    private void ReturnToSelection()
    {
        Invalidate();
        this.FindControl<ScrollViewer>("ImportScroll")!.Offset = default;
        rows.FirstOrDefault(row => row.IsSelected)?.FindControl<CheckBox>("SelectImportPlace")!.Focus();
    }

    private void Preview()
    {
        try
        {
            reviewed = HomeImportPlan.Create(baseline, catalog, rows.Where(row => row.IsSelected).Select(row => row.Choice).ToArray());
            var changes = this.FindControl<StackPanel>("ImportReviewChanges")!;
            changes.Children.Clear();
            this.FindControl<TextBlock>("ImportReviewSummary")!.Text = $"{reviewed.AddedPlaceIds.Count} new places · {reviewed.AddedBindings.Count} new bindings · {reviewed.Conflicts.Count} assignments kept elsewhere";
            foreach (var row in rows.Where(row => row.IsSelected))
            {
                var choice = row.Choice;
                var newPlace = reviewed.AddedPlaceIds.Contains(choice.PlaceId);
                var group = new StackPanel { Spacing = 6 };
                group.Children.Add(new TextBlock { Text = newPlace ? $"Add {choice.Name}" : $"Keep {baseline.Places.Single(place => place.Id == choice.PlaceId).Name}", TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold });
                foreach (var source in catalog.Places.Single(place => place.PlaceId == choice.PlaceId).Sources)
                {
                    var conflict = reviewed.Conflicts.FirstOrDefault(item => item.SourceId == source.SourceId && item.Role == source.Field);
                    var text = conflict is null ? $"{source.Name} · {source.Field}"
                        : $"{source.Name} · {source.Field}: stays with {baseline.Places.Single(place => place.Id == conflict.ExistingPlaceId).Name}. No reassignment.";
                    group.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Classes = { "muted" } });
                }
                changes.Children.Add(group);
            }
            var canAdd = reviewed.AddedPlaceIds.Count > 0 || reviewed.AddedBindings.Count > 0;
            this.FindControl<Button>("AddImportToDraft")!.IsEnabled = canAdd;
            Status(canAdd ? "Review the additions. Add to draft does not save; Save setup comes next." : "Nothing new to add. Existing assignments and missing bindings are unchanged.", false);
            Adapt();
            Dispatcher.UIThread.Post(() =>
            {
                var title = this.FindControl<TextBlock>("ImportReviewTitle")!;
                if (!Expanded)
                {
                    var scroll = this.FindControl<ScrollViewer>("ImportScroll")!;
                    if (scroll.Content is Visual content && title.TranslatePoint(default, content) is { } point)
                    { scroll.Offset = new Vector(0, point.Y); }
                }
                title.Focus();
            }, DispatcherPriority.Loaded);
        }
        catch (ArgumentException)
        {
            reviewed = null;
            this.FindControl<Button>("AddImportToDraft")!.IsEnabled = false;
            Status("The selection cannot be added. Use readable names up to 80 characters and keep the setup within 200 places and 2,000 bindings.", true);
            this.FindControl<TextBlock>("ImportStatus")!.Focus();
        }
    }

    private void Status(string text, bool problem)
    {
        var status = this.FindControl<TextBlock>("ImportStatus")!;
        status.Text = text;
        status[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(problem ? "ProblemBrush" : "MutedBrush");
    }
    private void Adapt()
    {
        var expanded = Expanded;
        var grid = this.FindControl<Grid>("ImportLayout")!;
        grid.ColumnDefinitions[1].Width = expanded ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        grid.ColumnSpacing = expanded ? 24 : 0;
        var review = this.FindControl<StackPanel>("ImportReview")!;
        Grid.SetColumn(review, expanded ? 1 : 0); Grid.SetRow(review, 0);
        review.IsVisible = expanded || reviewed is not null;
        this.FindControl<StackPanel>("ImportSelection")!.IsVisible = expanded || reviewed is null;
        this.FindControl<Button>("PreviewImport")!.IsVisible = expanded || reviewed is null;
        this.FindControl<Button>("EditImportSelection")!.IsVisible = !expanded && reviewed is not null;
        foreach (var name in new[] { "PreviewImport", "AddImportToDraft", "CancelImport", "EditImportSelection" })
        { this.FindControl<Button>(name)!.MaxWidth = Math.Max(100, Bounds.Width - 32); }
    }
    private bool Expanded => Bounds.Width >= 900 && this.FindControl<TextBlock>("ImportStatus")!.FontSize <= 20;
}
