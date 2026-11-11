using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Lucia.Companion.App.Controls;

/// <summary>
/// The app's four destinations. A bottom bar on narrow widths, a left rail with the Lucia mark on wide ones.
/// It only reports which destination was chosen; the shell decides what each one shows.
/// </summary>
public sealed partial class ShellNavigation : UserControl
{
    private static readonly ShellDestination[] Order = [ShellDestination.Home, ShellDestination.Story, ShellDestination.Lucia, ShellDestination.More];
    private bool laidOut;

    public IReadOnlyList<ShellDestination> Destinations => Order;
    public ShellDestination Selected { get; private set; } = ShellDestination.Home;
    public bool IsRail { get; private set; }
    public event Action<ShellDestination>? DestinationRequested;
    public event Action? TextMetricsChanged;

    public ShellNavigation()
    {
        AvaloniaXamlLoader.Load(this);
        for (var index = 0; index < Order.Length; index++)
        {
            var destination = Order[index];
            var button = ButtonFor(destination);
            foreach (var label in ((StackPanel)button.Content!).Children.OfType<TextBlock>())
            {
                label.PropertyChanged += (_, args) =>
                {
                    if (args.Property == TextBlock.FontSizeProperty || args.Property == TextBlock.FontFamilyProperty)
                    { TextMetricsChanged?.Invoke(); }
                };
            }
            ContextHelp.Set(button, destination switch
            {
                ShellDestination.Home => "Your places, current readings and things that need you.",
                ShellDestination.Story => "Explore the sample day and week. Connected history is not available yet.",
                ShellDestination.Lucia => "See reported requests and their current status.",
                _ => "Appearance, motion, Home setup and connection.",
            });
            AutomationProperties.SetPositionInSet(button, index + 1);
            AutomationProperties.SetSizeOfSet(button, Order.Length);
            button.Click += (_, _) => DestinationRequested?.Invoke(destination);
        }
        SetRail(false);
        Select(ShellDestination.Home);
    }

    public Button ButtonFor(ShellDestination destination) => this.FindControl<Button>($"Nav{destination}")!;

    public void Select(ShellDestination destination)
    {
        Selected = destination;
        foreach (var item in Order)
        {
            var button = ButtonFor(item);
            var selected = item == destination;
            button.Classes.Set("selected", selected);
            AutomationProperties.SetItemStatus(button, selected ? "Current page" : null);
            // Android's native provider does not expose ItemStatus. Keep current location in the portable name too.
            AutomationProperties.SetName(button, selected ? $"{item}, current page" : item.ToString());
        }
    }

    public void SetRail(bool rail)
    {
        if (laidOut && rail == IsRail) { return; }
        laidOut = true;
        IsRail = rail;
        var items = this.FindControl<Grid>("NavigationItems")!;
        items.ColumnDefinitions.Clear();
        items.RowDefinitions.Clear();
        items.ColumnSpacing = 0;
        items.RowSpacing = rail ? 8 : 0;
        items.Margin = rail ? new Thickness(8, 0) : new Thickness(4, 4, 4, 6);
        for (var index = 0; index < Order.Length; index++)
        {
            if (rail) { items.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); }
            else { items.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star)); }
            var button = ButtonFor(Order[index]);
            Grid.SetRow(button, rail ? index : 0);
            Grid.SetColumn(button, rail ? 0 : index);
            button.Classes.Set("rail", rail);
            foreach (var label in ((StackPanel)button.Content!).Children.OfType<TextBlock>())
            { label.TextWrapping = rail ? TextWrapping.NoWrap : TextWrapping.Wrap; }
        }
        this.FindControl<Border>("LuciaMark")!.IsVisible = rail;
        this.FindControl<Border>("NavigationFrame")!.BorderThickness = rail ? new Thickness(0, 0, 1, 0) : new Thickness(0, 1, 0, 0);
        // Native measurement can widen the rail for mapped text without shrinking or splitting its labels.
        Width = double.NaN;
        MinWidth = rail ? 88 : 0;
    }
}
