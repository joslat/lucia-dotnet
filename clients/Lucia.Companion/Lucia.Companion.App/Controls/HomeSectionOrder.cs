using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Named movement controls, usable without drag gestures. The view model owns the saved preference.</summary>
public sealed class HomeSectionOrder : StackPanel
{
    private readonly Dictionary<HomeSection, Grid> rows = [];
    private readonly Dictionary<HomeSection, Button> earlier = [];
    private readonly Dictionary<HomeSection, Button> later = [];
    private readonly StackPanel list = new() { Spacing = 8 };
    private IReadOnlyList<HomeSection> order = HomeSections.DefaultOrder;
    private readonly Button restore;
    public event Action<IReadOnlyList<HomeSection>>? OrderChanged;

    public HomeSectionOrder()
    {
        var names = new NameScope();
        NameScope.SetNameScope(this, names);
        Spacing = 12;
        Children.Add(new TextBlock { Text = "Move a section earlier or later. Changes save on this device.", Classes = { "muted" },
            [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13") });
        foreach (var section in HomeSections.DefaultOrder)
        {
            var label = HomeSections.Label(section);
            var row = new Grid { ColumnDefinitions = new("*,Auto,Auto"), ColumnSpacing = 8 };
            row.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            var up = MoveButton($"Move {label} earlier", true);
            var down = MoveButton($"Move {label} later", false);
            names.Register($"Move{section}Earlier", up);
            names.Register($"Move{section}Later", down);
            Grid.SetColumn(up, 1);
            Grid.SetColumn(down, 2);
            row.Children.Add(up);
            row.Children.Add(down);
            up.Click += (_, _) => Move(section, -1);
            down.Click += (_, _) => Move(section, 1);
            rows[section] = row;
            earlier[section] = up;
            later[section] = down;
        }
        Children.Add(list);
        restore = new Button { Content = new TextBlock { Text = "Restore default order", TextWrapping = TextWrapping.Wrap },
            Classes = { "theme" }, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(restore, "Restore default order");
        names.Register("RestoreSectionOrder", restore);
        restore.Click += (_, _) =>
        {
            OrderChanged?.Invoke(HomeSections.DefaultOrder);
            later[HomeSection.Places].Focus();
        };
        Children.Add(restore);
        SetOrder(order);
    }

    public void SetOrder(IEnumerable<HomeSection> sections)
    {
        order = HomeSections.ValidateOrder(sections);
        for (var index = 0; index < order.Count; index++)
        {
            var section = order[index];
            var row = rows[section];
            if (list.Children.IndexOf(row) != index)
            {
                list.Children.Remove(row);
                list.Children.Insert(index, row);
            }
            earlier[section].IsEnabled = index > 0;
            later[section].IsEnabled = index < order.Count - 1;
        }
        restore.IsEnabled = !order.SequenceEqual(HomeSections.DefaultOrder);
    }

    private void Move(HomeSection section, int step)
    {
        var updated = order.ToArray();
        var index = Array.IndexOf(updated, section);
        var target = index + step;
        if (target < 0 || target >= updated.Length) { return; }
        (updated[index], updated[target]) = (updated[target], updated[index]);
        OrderChanged?.Invoke(updated);
        var focus = step < 0 && earlier[section].IsEnabled ? earlier[section] : later[section].IsEnabled ? later[section] : earlier[section];
        focus.Focus();
        Dispatcher.UIThread.Post(() => focus.BringIntoView(), DispatcherPriority.Background);
    }

    private static Button MoveButton(string name, bool up)
    {
        var icon = new Avalonia.Controls.Shapes.Path { Width = 20, Height = 20, Stretch = Stretch.Uniform,
            Data = Geometry.Parse(up ? "M4,14 L12,6 20,14" : "M4,8 L12,16 20,8"), StrokeThickness = 1.7 };
        icon[!Avalonia.Controls.Shapes.Path.StrokeProperty] = new DynamicResourceExtension("TextBrush");
        var button = new Button { Content = icon, Classes = { "theme" }, MinWidth = 48, MinHeight = 48 };
        AutomationProperties.SetName(button, name);
        ToolTip.SetTip(button, name);
        return button;
    }
}
