using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia;
using Avalonia.VisualTree;
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
    private int editorFocusVersion;
    private int queuedEditorFocusVersion = -1;
    private bool? expandedLayout;
    private bool adapting;
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
        this.FindControl<ComboBox>("ZoneInput")!.ItemsSource = PlaceZones.Choices;
        this.FindControl<ListBox>("PlacesList")!.SelectionChanged += (_, _) => SelectFromList();
        this.FindControl<ComboBox>("PlaceChooser")!.SelectionChanged += (_, _) =>
            SelectId((this.FindControl<ComboBox>("PlaceChooser")!.SelectedItem as ComboBoxItem)?.Tag as string);
        this.FindControl<ComboBox>("PlaceChooser")!.DropDownClosed += (_, _) => InvalidateMeasure();
        this.FindControl<Button>("AddPlace")!.Click += (_, _) =>
        {
            if (!TryApplyCurrent()) { return; }
            RunEdit(() => RebuildList(draft.AddPlace("New place", PlaceIcon.Home)));
            this.FindControl<TextBox>("PlaceNameInput")!.Focus();
            this.FindControl<TextBox>("PlaceNameInput")!.SelectAll();
            var input = this.FindControl<TextBox>("PlaceNameInput")!;
            QueueEditorFocus(input, input, questionVisible: false);
        };
        this.FindControl<Button>("MoveEarlier")!.Click += (_, _) => Move(-1);
        this.FindControl<Button>("MoveLater")!.Click += (_, _) => Move(1);
        this.FindControl<Button>("RemovePlace")!.Click += (_, _) => ShowRemovalQuestion();
        this.FindControl<Button>("KeepPlace")!.Click += (_, _) => DismissRemovalQuestion();
        this.FindControl<Button>("ConfirmRemove")!.Click += (_, _) =>
        {
            if (selectedId is null) { return; }
            if (!RunEdit(() => { draft.RemovePlace(selectedId); RebuildList(draft.Places.FirstOrDefault()?.Id); })) { return; }
            var target = selectedId is null ? this.FindControl<Button>("AddPlace")!
                : this.FindControl<ComboBox>("PlaceChooser")!.IsVisible ? (Control)this.FindControl<ComboBox>("PlaceChooser")!
                : (Control)this.FindControl<ListBox>("PlacesList")!.SelectedItem!;
            QueueEditorFocus(target, target, questionVisible: false);
        };
        this.FindControl<Button>("SaveHome")!.Click += (_, _) =>
        {
            if (!TryApplyCurrent()) { return; }
            RunEdit(() =>
            {
                draft.SetHomeName(this.FindControl<TextBox>("HomeNameInput")!.Text ?? string.Empty);
                editorFocusVersion++;
                SaveRequested?.Invoke(draft.Build());
            });
        };
        this.FindControl<Button>("CancelEdit")!.Click += (_, _) => { editorFocusVersion++; CancelRequested?.Invoke(); };
        this.FindControl<Button>("StayInSetup")!.Click += (_, _) => ShowDiscardQuestion(false);
        this.FindControl<Button>("DiscardEdits")!.Click += (_, _) => { editorFocusVersion++; CancelRequested?.Invoke(); };
        KeyDown += (_, args) => { if (args.Key == Key.Escape) { RequestBack(); args.Handled = true; } };
        DetachedFromVisualTree += (_, _) => { editorFocusVersion++; InvalidateSharedLayout(); };
        _ = new PresentationVisibility(this, () => { if (!IsEffectivelyVisible) { editorFocusVersion++; } });
        RebuildList(draft.Places.FirstOrDefault()?.Id);
    }

    public void RequestBack()
    {
        if (this.FindControl<ContentControl>("ImportHost")! is { IsVisible: true, Content: HomeSharedLayoutScreen shared }) { shared.RequestBack(); return; }
        if (this.FindControl<ContentControl>("ImportHost")! is { IsVisible: true, Content: HomeImportScreen import }) { import.RequestBack(); return; }
        var removal = this.FindControl<StackPanel>("RemovalConfirmation")!;
        if (removal.IsVisible)
        {
            DismissRemovalQuestion();
            return;
        }
        if (this.FindControl<Border>("DiscardConfirmation")!.IsVisible) { ShowDiscardQuestion(false); return; }
        if (HasPendingChanges()) { ShowDiscardQuestion(true); }
        else { editorFocusVersion++; CancelRequested?.Invoke(); }
    }

    public void InvalidateSharedLayout()
    {
        if (this.FindControl<ContentControl>("ImportHost")!.Content is HomeSharedLayoutScreen review) { review.Controller.Invalidate(); }
    }

    private void ShowRemovalQuestion()
    {
        var place = draft.Places.FirstOrDefault(item => item.Id == selectedId);
        if (place is null) { return; }
        var question = this.FindControl<StackPanel>("RemovalConfirmation")!;
        this.FindControl<TextBlock>("RemovalQuestion")!.Text =
            $"Remove {place.Name}, its connections and observation bindings from the draft? Save setup applies the change.";
        question.IsVisible = true;
        QueueEditorFocus(this.FindControl<Button>("KeepPlace")!, question, questionVisible: true);
    }

    private void DismissRemovalQuestion()
    {
        this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible = false;
        var remove = this.FindControl<Button>("RemovePlace")!;
        QueueEditorFocus(remove, remove, questionVisible: false);
    }

    private void QueueEditorFocus(Control target, Control reveal, bool questionVisible, Control? nativeSelectionOwner = null)
    {
        var version = ++editorFocusVersion;
        queuedEditorFocusVersion = version;
        var placeId = selectedId;
        var expectedFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (version != editorFocusVersion || TopLevel.GetTopLevel(this) is not { } top
                    || !IsEffectivelyVisible || selectedId != placeId
                    || this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible != questionVisible
                    || this.FindControl<ContentControl>("ImportHost")!.IsVisible
                    || this.FindControl<Border>("DiscardConfirmation")!.IsVisible
                    || !target.IsEffectivelyVisible || !target.IsEffectivelyEnabled) { return; }
                var current = top.FocusManager?.GetFocusedElement();
                if (!ReferenceEquals(current, expectedFocus) && !ReferenceEquals(current, target)
                    && current is Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } focused
                    && !(nativeSelectionOwner is not null && (ReferenceEquals(focused, nativeSelectionOwner)
                        || nativeSelectionOwner.IsVisualAncestorOf(focused)))) { return; }
                reveal.BringIntoView();
                target.Focus();
                target.BringIntoView();
            }
            finally { if (queuedEditorFocusVersion == version) { queuedEditorFocusVersion = -1; } }
        }, DispatcherPriority.Loaded);
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
        this.FindControl<Grid>("EditorGrid")!.IsVisible = false;
        this.FindControl<StackPanel>("EditorCommands")!.IsVisible = false;
        var host = this.FindControl<ContentControl>("ImportHost")!;
        host.Content = review; host.IsVisible = true;
    }

    private void OpenImport(HomeCatalog catalog)
    {
        if (!TryApplyCurrent() || !RunEdit(() => draft.SetHomeName(this.FindControl<TextBox>("HomeNameInput")!.Text ?? string.Empty))) { return; }
        editorFocusVersion++;
        this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible = false;
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
        this.FindControl<Grid>("EditorGrid")!.IsVisible = false;
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
        this.FindControl<Grid>("EditorGrid")!.IsVisible = true;
        this.FindControl<StackPanel>("EditorCommands")!.IsVisible = true;
        this.FindControl<Button>(wasShared ? "ReviewSharedSetup" : "ImportPlaces")!.Focus();
    }

    private bool HasPendingChanges()
    {
        if (draft.HasChanges || this.FindControl<TextBox>("HomeNameInput")!.Text != draft.HomeName) { return true; }
        var place = draft.Places.FirstOrDefault(place => place.Id == selectedId);
        if (place is null) { return false; }
        if (this.FindControl<TextBox>("PlaceNameInput")!.Text != place.Name ||
            !Equals(this.FindControl<ComboBox>("IconInput")!.SelectedItem, place.Icon) ||
            PlaceZones.FromChoice(this.FindControl<ComboBox>("ZoneInput")!.SelectedIndex) != place.Zone) { return true; }
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
        var listOwner = this.FindControl<ListBox>("PlacesList")!;
        var chooserOwner = this.FindControl<ComboBox>("PlaceChooser")!;
        var nativeSelectionOwner = listOwner.IsKeyboardFocusWithin ? (Control)listOwner
            : chooserOwner.IsKeyboardFocusWithin || chooserOwner.IsDropDownOpen ? chooserOwner : null;
        if (!TryApplyCurrent(nativeSelectionOwner))
        {
            filling = true;
            var list = this.FindControl<ListBox>("PlacesList")!;
            list.SelectedItem = list.Items.OfType<ListBoxItem>().FirstOrDefault(item => Equals(item.Tag, selectedId));
            var chooser = this.FindControl<ComboBox>("PlaceChooser")!;
            chooser.SelectedItem = chooser.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, selectedId));
            filling = false;
            chooser.IsDropDownOpen = false;
            return;
        }
        // Keep native item identity and keyboard focus when only the selected place changes.
        foreach (var item in this.FindControl<ListBox>("PlacesList")!.Items.OfType<ListBoxItem>())
        { item.Content = draft.Places.First(place => place.Id == (string)item.Tag!).Name; }
        foreach (var item in this.FindControl<ComboBox>("PlaceChooser")!.Items.OfType<ComboBoxItem>())
        { item.Content = draft.Places.First(place => place.Id == (string)item.Tag!).Name; }
        FillSelection(nextId);
    }

    private void RebuildList(string? id)
    {
        filling = true;
        var list = this.FindControl<ListBox>("PlacesList")!;
        var items = draft.Places.Select(place => new ListBoxItem { Content = place.Name, Tag = place.Id, MinHeight = 48 }).ToArray();
        list.ItemsSource = items;
        var chooser = this.FindControl<ComboBox>("PlaceChooser")!;
        var choices = draft.Places.Select(place => new ComboBoxItem { Content = place.Name, Tag = place.Id }).ToArray();
        chooser.ItemsSource = choices;
        filling = false;
        FillSelection(id);
    }

    private void FillSelection(string? id)
    {
        filling = true;
        var list = this.FindControl<ListBox>("PlacesList")!;
        list.SelectedItem = list.Items.OfType<ListBoxItem>().FirstOrDefault(item => Equals(item.Tag, id));
        var chooser = this.FindControl<ComboBox>("PlaceChooser")!;
        chooser.SelectedItem = chooser.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, id));
        filling = false;
        Populate(id);
        if (list.SelectedItem is { } selected) { list.ScrollIntoView(selected); }
        InvalidateMeasure();
    }

    private void Populate(string? id)
    {
        editorFocusVersion++;
        selectedId = id;
        var place = draft.Places.FirstOrDefault(candidate => candidate.Id == id);
        this.FindControl<Border>("PlaceFormCard")!.IsVisible = place is not null;
        this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible = false;
        if (place is null) { return; }
        this.FindControl<TextBox>("PlaceNameInput")!.Text = place.Name;
        this.FindControl<ComboBox>("IconInput")!.SelectedItem = place.Icon;
        this.FindControl<ComboBox>("ZoneInput")!.SelectedIndex = PlaceZones.ChoiceIndex(place.Zone);
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

    private bool TryApplyCurrent(Control? nativeSelectionOwner = null)
    {
        if (selectedId is null) { return true; }
        var name = this.FindControl<TextBox>("PlaceNameInput")!.Text;
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowPlaceError("Enter a place name.");
            RevealInvalidPlace(nativeSelectionOwner);
            return false;
        }

        var applied = RunEdit(() =>
        {
            var icon = this.FindControl<ComboBox>("IconInput")!.SelectedItem is PlaceIcon selected ? selected : PlaceIcon.Home;
            draft.UpdatePlace(selectedId, name, icon, PlaceZones.FromChoice(this.FindControl<ComboBox>("ZoneInput")!.SelectedIndex));
            draft.SetConnections(selectedId, this.FindControl<StackPanel>("ConnectionsList")!.Children.OfType<CheckBox>()
                .Where(check => check.IsChecked == true).Select(check => (string)check.Tag!));
        }, ShowPlaceError);
        if (!applied) { RevealInvalidPlace(nativeSelectionOwner); }
        return applied;
    }

    private void RevealInvalidPlace(Control? nativeSelectionOwner) => QueueEditorFocus(this.FindControl<TextBox>("PlaceNameInput")!,
        this.FindControl<TextBlock>("ErrorText")!, this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible, nativeSelectionOwner);

    private bool RunEdit(Action action, Action<string>? reportError = null)
    {
        try
        {
            action();
            this.FindControl<TextBlock>("ErrorText")!.IsVisible = false;
            this.FindControl<TextBlock>("HomeErrorText")!.IsVisible = false;
            return true;
        }
        catch (ArgumentException error) { (reportError ?? ShowError)(error.Message.Split(Environment.NewLine)[0]); return false; }
    }

    private void ShowError(string message)
    {
        var error = this.FindControl<TextBlock>("HomeErrorText")!;
        error.Text = message;
        error.IsVisible = true;
        error.BringIntoView();
    }

    private void ShowPlaceError(string message)
    {
        var error = this.FindControl<TextBlock>("ErrorText")!;
        error.Text = message;
        error.IsVisible = true;
        error.BringIntoView();
    }

    private void Move(int direction)
    {
        if (selectedId is null || !TryApplyCurrent()) { return; }
        RunEdit(() => { draft.MovePlace(selectedId, direction); RebuildList(selectedId); });
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Adapt(availableSize);
        return base.MeasureOverride(availableSize);
    }

    private void Adapt(Size availableSize)
    {
        if (adapting || availableSize.Width <= 0 || availableSize.Height <= 0) { return; }
        adapting = true;
        try
        {
            const double columnGap = 16;
            var layout = (Grid)Content!;
            var grid = this.FindControl<Grid>("EditorGrid")!;
            var card = this.FindControl<Border>("PlaceListCard")!;
            var master = (Grid)card.Child!;
            var header = this.FindControl<StackPanel>("MasterHeader")!;
            var management = this.FindControl<StackPanel>("PlaceManagement")!;
            var contentWidth = Math.Max(0, availableSize.Width - layout.Margin.Left - layout.Margin.Right);
            var masterWidth = Math.Max(0, (contentWidth - columnGap) / 3
                - card.Padding.Left - card.Padding.Right - card.BorderThickness.Left - card.BorderThickness.Right);
            header.Measure(new Size(masterWidth, double.PositiveInfinity));
            management.Measure(new Size(masterWidth, double.PositiveInfinity));
            var commands = this.FindControl<StackPanel>("EditorCommands")!;
            commands.Measure(new Size(contentWidth, double.PositiveInfinity));
            var bodyHeight = availableSize.Height - commands.DesiredSize.Height - layout.Margin.Top - layout.Margin.Bottom - layout.RowSpacing;
            var list = this.FindControl<ListBox>("PlacesList")!;
            var requiredHeight = header.DesiredSize.Height + management.DesiredSize.Height + list.MinHeight + master.RowSpacing * 2
                + card.Padding.Top + card.Padding.Bottom + card.BorderThickness.Top + card.BorderThickness.Bottom;
            var chooser = this.FindControl<ComboBox>("PlaceChooser")!;
            // Finishing the current chooser interaction takes precedence over gaining room after the IME closes.
            var expanded = availableSize.Width >= 800 && bodyHeight >= requiredHeight
                && !(expandedLayout == false && chooser.IsDropDownOpen);
            var focusQueued = queuedEditorFocusVersion == editorFocusVersion;
            var transferFocus = !focusQueued && expandedLayout != expanded && (list.IsKeyboardFocusWithin || chooser.IsKeyboardFocusWithin);
            // Capture focus before hiding or detaching the old host clears the native focus manager.
            var managementFocus = !focusQueued && expandedLayout != expanded && management.IsKeyboardFocusWithin
                ? TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control : null;
            grid.ColumnDefinitions[0].Width = expanded ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            grid.ColumnSpacing = expanded ? columnGap : 0;
            card.IsVisible = expanded;
            list.IsVisible = expanded;
            chooser.IsVisible = !expanded && draft.Places.Count > 0;
            this.FindControl<StackPanel>("CompactPlaceContext")!.IsVisible = chooser.IsVisible;
            var masterHost = this.FindControl<ContentControl>("MasterManagementHost")!;
            var compactHost = this.FindControl<ContentControl>("CompactManagementHost")!;
            var activeHost = expanded ? masterHost : compactHost;
            if (!ReferenceEquals(activeHost.Content, management))
            {
                masterHost.Content = null; compactHost.Content = null;
                activeHost.Content = management;
            }
            expandedLayout = expanded;
            if (managementFocus is not null)
            {
                QueueEditorFocus(managementFocus, managementFocus, this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible);
            }
            else if (transferFocus && selectedId is not null)
            {
                var target = expanded ? (Control)list.SelectedItem! : chooser;
                QueueEditorFocus(target, target, this.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible);
            }
        }
        finally { adapting = false; }
    }
}
