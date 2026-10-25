using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.App.Controls;
using Lucia.Companion.Core;
using Lucia.Companion.Connection;
using Lucia.Companion.App.Configuration;

namespace Lucia.Companion.App.Screens;

public sealed partial class HomeEditorScreen : UserControl
{
    private readonly HomeEditorViewModel draft;
    private readonly IReadOnlyList<ObservationSource> sources;
    private string? selectedId;
    private bool filling;
    private readonly Func<Func<HomeTopology>, SharedLayoutAdoptionController>? sharedLayoutFactory;
    public event Action<HomeTopology>? SaveRequested;
    public event Action? CancelRequested;
    public event Action? ConnectionRequested;

    public HomeEditorScreen() : this(SampleHome.Create()) { }

    public HomeEditorScreen(HomeTopology home, IReadOnlyList<ObservationSource>? sources = null, HomeCatalog? importCatalog = null,
        Func<Func<HomeTopology>, SharedLayoutAdoptionController>? sharedLayoutFactory = null)
    {
        draft = new(home);
        this.sources = Array.AsReadOnly((sources ?? []).ToArray());
        this.sharedLayoutFactory = sharedLayoutFactory;
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Button>("ImportPlaces")!.IsVisible = importCatalog is not null;
        this.FindControl<Button>("ImportPlaces")!.Click += (_, _) => { if (importCatalog is not null) { OpenImport(importCatalog); } };
        this.FindControl<Button>("ReviewSharedSetup")!.IsVisible = sharedLayoutFactory is not null;
        this.FindControl<Button>("ReviewSharedSetup")!.Click += (_, _) => OpenSharedSetup();
        var bindings = this.FindControl<SourceBindingPicker>("BindingPicker")!;
        bindings.BindingRequested += (source, role) =>
        {
            if (selectedId is not null && RunEdit(() => draft.BindSource(selectedId, source, role), bindings.ShowError)) { RefreshBindings(); }
        };
        bindings.RemovalRequested += (sourceId, role) =>
        {
            if (selectedId is not null && RunEdit(() => draft.UnbindSource(selectedId, sourceId, role), bindings.ShowError)) { RefreshBindings(); }
        };
        this.FindControl<TextBox>("HomeNameInput")!.Text = home.Name;
        this.FindControl<ComboBox>("IconInput")!.ItemsSource = Enum.GetValues<PlaceIcon>();
        this.FindControl<ListBox>("PlacesList")!.SelectionChanged += (_, _) => SelectFromList();
        this.FindControl<ComboBox>("PlaceChooser")!.SelectionChanged += (_, _) =>
            SelectId((this.FindControl<ComboBox>("PlaceChooser")!.SelectedItem as ComboBoxItem)?.Tag as string);
        this.FindControl<Button>("AddPlace")!.Click += (_, _) =>
        {
            if (!TryApplyCurrent()) { return; }
            RunEdit(() => RebuildList(draft.AddPlace("New place", PlaceIcon.Home)));
            this.FindControl<TextBox>("PlaceNameInput")!.Focus();
            this.FindControl<TextBox>("PlaceNameInput")!.SelectAll();
        };
        this.FindControl<Button>("MoveEarlier")!.Click += (_, _) => Move(-1);
        this.FindControl<Button>("MoveLater")!.Click += (_, _) => Move(1);
        this.FindControl<Button>("RemovePlace")!.Click += (_, _) => this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible = true;
        this.FindControl<Button>("KeepPlace")!.Click += (_, _) => this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible = false;
        this.FindControl<Button>("ConfirmRemove")!.Click += (_, _) =>
        {
            if (selectedId is null) { return; }
            RunEdit(() => { draft.RemovePlace(selectedId); RebuildList(draft.Places.FirstOrDefault()?.Id); });
        };
        this.FindControl<Button>("SaveHome")!.Click += (_, _) =>
        {
            if (!TryApplyCurrent()) { return; }
            RunEdit(() =>
            {
                draft.SetHomeName(this.FindControl<TextBox>("HomeNameInput")!.Text ?? string.Empty);
                SaveRequested?.Invoke(draft.Build());
            });
        };
        this.FindControl<Button>("CancelEdit")!.Click += (_, _) => CancelRequested?.Invoke();
        this.FindControl<Button>("StayInSetup")!.Click += (_, _) => ShowDiscardQuestion(false);
        this.FindControl<Button>("DiscardEdits")!.Click += (_, _) => CancelRequested?.Invoke();
        KeyDown += (_, args) => { if (args.Key == Key.Escape) { RequestBack(); args.Handled = true; } };
        SizeChanged += (_, _) => Adapt();
        DetachedFromVisualTree += (_, _) => InvalidateSharedLayout();
        RebuildList(draft.Places.FirstOrDefault()?.Id);
    }

    public void RequestBack()
    {
        if (this.FindControl<ContentControl>("ImportHost")! is { IsVisible: true, Content: HomeSharedLayoutScreen shared }) { shared.RequestBack(); return; }
        if (this.FindControl<ContentControl>("ImportHost")! is { IsVisible: true, Content: HomeImportScreen import }) { import.RequestBack(); return; }
        var removal = this.FindControl<StackPanel>("RemovalConfirmation")!;
        if (removal.IsVisible)
        {
            removal.IsVisible = false;
            this.FindControl<Button>("RemovePlace")!.Focus();
            return;
        }
        if (this.FindControl<Border>("DiscardConfirmation")!.IsVisible) { ShowDiscardQuestion(false); return; }
        if (HasPendingChanges()) { ShowDiscardQuestion(true); }
        else { CancelRequested?.Invoke(); }
    }

    public void InvalidateSharedLayout()
    {
        if (this.FindControl<ContentControl>("ImportHost")!.Content is HomeSharedLayoutScreen review) { review.Controller.Invalidate(); }
    }

    private void OpenSharedSetup()
    {
        if (sharedLayoutFactory is null || this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible
            || !TryApplyCurrent() || !RunEdit(() => draft.SetHomeName(this.FindControl<TextBox>("HomeNameInput")!.Text ?? string.Empty))) { return; }
        var review = new HomeSharedLayoutScreen(sharedLayoutFactory(draft.Build));
        review.CancelRequested += CloseImport;
        review.ConnectionRequested += () => ConnectionRequested?.Invoke();
        review.AddRequested += updated =>
        {
            if (!RunEdit(() => draft.ReplaceWithReviewedSetup(updated))) { return; }
            this.FindControl<TextBox>("HomeNameInput")!.Text = updated.Name;
            RebuildList(updated.Places.FirstOrDefault()?.Id);
            this.FindControl<TextBlock>("ImportDraftStatus")!.Text = "Shared setup added to this draft. Review names, connections and bindings, then Save setup to keep it on this device.";
            this.FindControl<TextBlock>("ImportDraftStatus")!.IsVisible = true;
            CloseImport();
        };
        this.FindControl<ScrollViewer>("EditorScroll")!.IsVisible = false;
        this.FindControl<StackPanel>("EditorCommands")!.IsVisible = false;
        var host = this.FindControl<ContentControl>("ImportHost")!;
        host.Content = review; host.IsVisible = true;
    }

    private void OpenImport(HomeCatalog catalog)
    {
        if (!TryApplyCurrent() || !RunEdit(() => draft.SetHomeName(this.FindControl<TextBox>("HomeNameInput")!.Text ?? string.Empty))) { return; }
        var import = new HomeImportScreen(draft.Build(), catalog);
        import.CancelRequested += CloseImport;
        import.ApplyRequested += plan =>
        {
            try
            {
                draft.ApplyImport(plan);
                RebuildList(plan.AddedPlaceIds.FirstOrDefault() ?? selectedId ?? draft.Places.FirstOrDefault()?.Id);
                this.FindControl<TextBlock>("ImportDraftStatus")!.Text = "Places added to this draft. Review their setup, then Save setup to keep them.";
                this.FindControl<TextBlock>("ImportDraftStatus")!.IsVisible = true;
                CloseImport();
            }
            catch (InvalidOperationException) { CloseImport(); ShowError("The draft changed. Open import and review the additions again."); }
        };
        this.FindControl<ScrollViewer>("EditorScroll")!.IsVisible = false;
        this.FindControl<StackPanel>("EditorCommands")!.IsVisible = false;
        var host = this.FindControl<ContentControl>("ImportHost")!;
        host.Content = import; host.IsVisible = true;
    }

    private void CloseImport()
    {
        var host = this.FindControl<ContentControl>("ImportHost")!;
        var wasShared = host.Content is HomeSharedLayoutScreen;
        if (host.Content is HomeSharedLayoutScreen shared) { shared.Dispose(); }
        host.IsVisible = false; host.Content = null;
        this.FindControl<ScrollViewer>("EditorScroll")!.IsVisible = true;
        this.FindControl<StackPanel>("EditorCommands")!.IsVisible = true;
        this.FindControl<Button>(wasShared ? "ReviewSharedSetup" : "ImportPlaces")!.Focus();
    }

    private bool HasPendingChanges()
    {
        if (draft.HasChanges || this.FindControl<TextBox>("HomeNameInput")!.Text != draft.HomeName) { return true; }
        var place = draft.Places.FirstOrDefault(place => place.Id == selectedId);
        if (place is null) { return false; }
        if (this.FindControl<TextBox>("PlaceNameInput")!.Text != place.Name ||
            !Equals(this.FindControl<ComboBox>("IconInput")!.SelectedItem, place.Icon)) { return true; }
        var neighbors = draft.Connections.Where(edge => edge.FromPlaceId == place.Id || edge.ToPlaceId == place.Id)
            .Select(edge => edge.FromPlaceId == place.Id ? edge.ToPlaceId : edge.FromPlaceId).ToHashSet(StringComparer.Ordinal);
        return !neighbors.SetEquals(this.FindControl<StackPanel>("ConnectionsList")!.Children.OfType<CheckBox>()
            .Where(check => check.IsChecked == true).Select(check => (string)check.Tag!));
    }

    private void ShowDiscardQuestion(bool show)
    {
        this.FindControl<Border>("DiscardConfirmation")!.IsVisible = show;
        this.FindControl<Grid>("EditorGrid")!.IsEnabled = !show;
        this.FindControl<Button>("SaveHome")!.IsEnabled = !show;
        this.FindControl<Button>("CancelEdit")!.IsEnabled = !show;
        this.FindControl<WrapPanel>("EditCommands")!.IsVisible = !show;
        this.FindControl<Button>(show ? "StayInSetup" : "CancelEdit")!.Focus();
    }

    public void WarnAboutReplacement()
    {
        this.FindControl<TextBlock>("ReplacementWarning")!.IsVisible = true;
        this.FindControl<Button>("SaveHome")!.Content = "Replace saved setup";
    }

    private void SelectFromList()
    {
        var nextId = (this.FindControl<ListBox>("PlacesList")!.SelectedItem as ListBoxItem)?.Tag as string;
        SelectId(nextId);
    }

    private void SelectId(string? nextId)
    {
        if (filling) { return; }
        if (nextId is null || nextId == selectedId) { return; }
        if (!TryApplyCurrent())
        {
            filling = true;
            var list = this.FindControl<ListBox>("PlacesList")!;
            list.SelectedItem = list.Items.OfType<ListBoxItem>().FirstOrDefault(item => Equals(item.Tag, selectedId));
            var chooser = this.FindControl<ComboBox>("PlaceChooser")!;
            chooser.SelectedItem = chooser.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, selectedId));
            filling = false;
            return;
        }
        RebuildList(nextId);
    }

    private void RebuildList(string? id)
    {
        filling = true;
        var list = this.FindControl<ListBox>("PlacesList")!;
        var items = draft.Places.Select(place => new ListBoxItem { Content = place.Name, Tag = place.Id, MinHeight = 48 }).ToArray();
        list.ItemsSource = items;
        list.SelectedItem = items.FirstOrDefault(item => Equals(item.Tag, id));
        var chooser = this.FindControl<ComboBox>("PlaceChooser")!;
        var choices = draft.Places.Select(place => new ComboBoxItem { Content = place.Name, Tag = place.Id }).ToArray();
        chooser.ItemsSource = choices;
        chooser.SelectedItem = choices.FirstOrDefault(item => Equals(item.Tag, id));
        filling = false;
        Populate(id);
    }

    private void Populate(string? id)
    {
        selectedId = id;
        var place = draft.Places.FirstOrDefault(candidate => candidate.Id == id);
        this.FindControl<Border>("PlaceFormCard")!.IsVisible = place is not null;
        this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible = false;
        if (place is null) { return; }
        this.FindControl<TextBox>("PlaceNameInput")!.Text = place.Name;
        this.FindControl<ComboBox>("IconInput")!.SelectedItem = place.Icon;
        var neighborIds = draft.Connections.Where(edge => edge.FromPlaceId == id || edge.ToPlaceId == id)
            .Select(edge => edge.FromPlaceId == id ? edge.ToPlaceId : edge.FromPlaceId).ToHashSet(StringComparer.Ordinal);
        var connections = this.FindControl<StackPanel>("ConnectionsList")!;
        connections.Children.Clear();
        foreach (var neighbor in draft.Places.Where(candidate => candidate.Id != id))
        {
            connections.Children.Add(new CheckBox { Content = neighbor.Name, Tag = neighbor.Id, IsChecked = neighborIds.Contains(neighbor.Id), MinHeight = 48 });
        }
        RefreshBindings();

        var index = draft.Places.ToList().FindIndex(candidate => candidate.Id == id);
        this.FindControl<Button>("MoveEarlier")!.IsEnabled = index > 0;
        this.FindControl<Button>("MoveLater")!.IsEnabled = index < draft.Places.Count - 1;
    }

    private void RefreshBindings()
    {
        if (selectedId is not null) { this.FindControl<SourceBindingPicker>("BindingPicker")!.SetContext(selectedId, sources, draft.Bindings); }
    }

    private bool TryApplyCurrent()
    {
        if (selectedId is null) { return true; }
        var name = this.FindControl<TextBox>("PlaceNameInput")!.Text;
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Enter a place name before saving or choosing another place.");
            return false;
        }

        return RunEdit(() =>
        {
            var icon = this.FindControl<ComboBox>("IconInput")!.SelectedItem is PlaceIcon selected ? selected : PlaceIcon.Home;
            draft.UpdatePlace(selectedId, name, icon);
            draft.SetConnections(selectedId, this.FindControl<StackPanel>("ConnectionsList")!.Children.OfType<CheckBox>()
                .Where(check => check.IsChecked == true).Select(check => (string)check.Tag!));
        });
    }

    private bool RunEdit(Action action, Action<string>? reportError = null)
    {
        try { action(); this.FindControl<TextBlock>("ErrorText")!.IsVisible = false; return true; }
        catch (ArgumentException error) { (reportError ?? ShowError)(error.Message.Split(Environment.NewLine)[0]); return false; }
    }

    private void ShowError(string message)
    {
        var error = this.FindControl<TextBlock>("ErrorText")!;
        error.Text = message;
        error.IsVisible = true;
    }

    private void Move(int direction)
    {
        if (selectedId is null || !TryApplyCurrent()) { return; }
        RunEdit(() => { draft.MovePlace(selectedId, direction); RebuildList(selectedId); });
    }

    private void Adapt()
    {
        var expanded = Bounds.Width >= 800;
        var grid = this.FindControl<Grid>("EditorGrid")!;
        grid.ColumnDefinitions[1].Width = expanded ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        grid.ColumnSpacing = expanded ? 16 : 0;
        this.FindControl<ListBox>("PlacesList")!.IsVisible = expanded;
        this.FindControl<ComboBox>("PlaceChooser")!.IsVisible = !expanded;
        var form = this.FindControl<Border>("PlaceFormCard")!;
        Grid.SetColumn(form, expanded ? 1 : 0);
        Grid.SetRow(form, expanded ? 0 : 1);
    }
}
