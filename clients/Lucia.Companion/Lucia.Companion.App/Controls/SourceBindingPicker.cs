using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

/// <summary>Displays discovered source choices and existing metadata. It never fetches readings or changes devices.</summary>
public sealed class SourceBindingPicker : UserControl
{
    private readonly ComboBox source = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 48 };
    private readonly ComboBox role = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 48 };
    private readonly Button add = new() { Content = "Bind source", Classes = { "theme" }, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly StackPanel rows = new() { Spacing = 12 };
    private readonly TextBlock status = new() { Classes = { "muted" } };
    private readonly TextBlock error = new() { IsVisible = false, Focusable = true, TextWrapping = TextWrapping.Wrap };
    public event Action<ObservationSource, ObservationRole>? BindingRequested;
    public event Action<string, ObservationRole>? RemovalRequested;

    public SourceBindingPicker()
    {
        var names = new NameScope();
        NameScope.SetNameScope(this, names);
        Focusable = true;
        AutomationProperties.SetName(this, "Observation bindings");
        names.Register("BindingSource", source);
        names.Register("BindingRole", role);
        names.Register("AddBinding", add);
        names.Register("BindingRows", rows);
        names.Register("BindingError", error);
        error[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("ProblemBrush");
        AutomationProperties.SetName(source, "Observation source");
        AutomationProperties.SetName(role, "Source observation role");
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Observation bindings", Classes = { "section" }, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Choose which source reports a reading here. Physical neighbors and source ownership are separate.", Classes = { "muted" } });
        panel.Children.Add(status);
        panel.Children.Add(rows);
        panel.Children.Add(error);
        panel.Children.Add(new TextBlock { Text = "Source" });
        panel.Children.Add(source);
        panel.Children.Add(new TextBlock { Text = "Reports" });
        panel.Children.Add(role);
        panel.Children.Add(add);
        Content = panel;
        source.SelectionChanged += (_, _) =>
        {
            error.IsVisible = false;
            role.ItemsSource = (source.SelectedItem as ComboBoxItem)?.Tag is ObservationSource selected ? selected.SupportedRoles : [];
            role.SelectedIndex = -1;
            RefreshAdd();
        };
        role.SelectionChanged += (_, _) => { error.IsVisible = false; RefreshAdd(); };
        add.Click += (_, _) =>
        {
            if ((source.SelectedItem as ComboBoxItem)?.Tag is ObservationSource selected && role.SelectedItem is ObservationRole selectedRole)
            {
                BindingRequested?.Invoke(selected, selectedRole);
                if (!error.IsVisible) { source.Focus(); }
            }
        };
        RefreshAdd();
    }

    public void SetContext(string placeId, IReadOnlyList<ObservationSource> sources, IEnumerable<ObservationBinding> bindings)
    {
        error.IsVisible = false;
        source.ItemsSource = sources.Select(item => new ComboBoxItem { Content = new TextBlock { Text = item.Name, TextWrapping = TextWrapping.Wrap }, Tag = item }).ToArray();
        source.SelectedIndex = -1;
        role.ItemsSource = Array.Empty<ObservationRole>();
        source.IsEnabled = sources.Count > 0;
        status.Text = sources.Count == 0 ? "No sources in current discovery. Connect an authorized source to add bindings. Existing bindings are kept for review."
            : "Available sources come from the current discovery. Adding a binding does not create a reading or grant access.";
        rows.Children.Clear();
        foreach (var binding in bindings.Where(binding => binding.PlaceId == placeId))
        {
            var known = sources.FirstOrDefault(item => item.Id == binding.SourceId);
            var row = new StackPanel { Spacing = 6 };
            row.Children.Add(new TextBlock { Text = $"{binding.Role} · {known?.Name ?? binding.SourceId}", TextWrapping = TextWrapping.Wrap });
            if (known is null || !known.SupportedRoles.Contains(binding.Role))
            {
                row.Children.Add(new TextBlock { Text = "Source or role is not in current discovery. Binding kept; no current reading is inferred.", Classes = { "muted" },
                    [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13") });
            }
            var remove = new Button { Content = "Remove binding", Classes = { "theme" }, HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetName(remove, $"Remove {binding.Role} binding for {known?.Name ?? binding.SourceId}");
            remove.Click += (_, _) =>
            {
                RemovalRequested?.Invoke(binding.SourceId, binding.Role);
                if (!error.IsVisible) { if (source.IsEnabled) { source.Focus(); } else { Focus(); } }
            };
            row.Children.Add(remove);
            rows.Children.Add(row);
        }
        RefreshAdd();
    }

    public void ShowError(string message)
    {
        error.Text = message;
        error.IsVisible = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (error.IsVisible && TopLevel.GetTopLevel(this) is not null) { error.Focus(); error.BringIntoView(); }
        }, DispatcherPriority.Background);
    }

    private void RefreshAdd()
    {
        role.IsEnabled = (source.SelectedItem as ComboBoxItem)?.Tag is ObservationSource;
        add.IsEnabled = role.IsEnabled && role.SelectedItem is ObservationRole;
    }
}
