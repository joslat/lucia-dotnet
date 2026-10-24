using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Screens;

public sealed partial class HomeEditorScreen : UserControl
{
    private readonly HomeEditorViewModel draft;
    private string? selectedId;
    private bool filling;
    public event Action<HomeTopology>? SaveRequested;
    public event Action? CancelRequested;

    public HomeEditorScreen() : this(SampleHome.Create()) { }

    public HomeEditorScreen(HomeTopology home)
    {
        draft = new(home);
        AvaloniaXamlLoader.Load(this);
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
        RebuildList(draft.Places.FirstOrDefault()?.Id);
    }

    public void RequestBack()
    {
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

        var index = draft.Places.ToList().FindIndex(candidate => candidate.Id == id);
        this.FindControl<Button>("MoveEarlier")!.IsEnabled = index > 0;
        this.FindControl<Button>("MoveLater")!.IsEnabled = index < draft.Places.Count - 1;
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

    private bool RunEdit(Action action)
    {
        try { action(); this.FindControl<TextBlock>("ErrorText")!.IsVisible = false; return true; }
        catch (ArgumentException error) { ShowError(error.Message.Split(Environment.NewLine)[0]); return false; }
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
