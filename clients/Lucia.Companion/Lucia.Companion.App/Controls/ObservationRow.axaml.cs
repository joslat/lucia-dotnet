using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

public sealed partial class ObservationRow : UserControl
{
    public ObservationRow() => AvaloniaXamlLoader.Load(this);

    public ObservationRow(Observation observation) : this()
    {
        ArgumentNullException.ThrowIfNull(observation);
        this.FindControl<TextBlock>("LabelText")!.Text = observation.Label;
        var icon = this.FindControl<Avalonia.Controls.Shapes.Path>("ReadingIcon")!;
        icon.Data = ReadingIcons.For(observation.Role);
        icon.IsVisible = true;
        this.FindControl<TextBlock>("ValueText")!.Text = observation.CurrentText;
        this.FindControl<TextBlock>("PreviousText")!.Text = observation.PreviousText;
        this.FindControl<TextBlock>("PreviousText")!.IsVisible = observation.PreviousText.Length > 0;
        this.FindControl<TextBlock>("TimeText")!.Text = observation.PreviousText.Length == 0 ? observation.TimestampText : string.Empty;
        this.FindControl<TextBlock>("TimeText")!.IsVisible = observation.PreviousText.Length == 0;
        SetValue(AutomationProperties.NameProperty,
            $"{observation.Label}: {observation.CurrentText}. {observation.PreviousText} {observation.TimestampText}");
    }
}
