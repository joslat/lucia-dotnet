using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Layout;
using Lucia.Companion.Connection;

namespace Lucia.Companion.App.Controls;

/// <summary>Reported request phases, never a simulated execution timer.</summary>
public sealed class LuciaStagePath : UserControl
{
    private readonly TextBlock title = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock identity = Small();
    private readonly TextBlock timing = Small();
    private readonly TextBlock explanation = Small();
    private readonly LuciaStageTrack track = new();
    private readonly Grid phases = new();
    private readonly TextBlock[] labels = [Small(), Small(), Small()];
    private ActivityRequest? rendered;
    public LuciaStagePath()
    {
        var content = new StackPanel { Spacing = 5 };
        content.Children.Add(identity); content.Children.Add(title);
        phases.Children.Add(track);
        foreach (var (label, index) in new[] { ("Received", 0), ("Processing", 1), ("Response", 2) })
        {
            var text = labels[index]; text.Text = label; text.TextWrapping = TextWrapping.NoWrap;
            text.VerticalAlignment = VerticalAlignment.Center; phases.Children.Add(text);
        }
        ArrangePhases(false);
        content.Children.Add(phases); content.Children.Add(timing); content.Children.Add(explanation);
        Content = content;
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var label in labels) { label.Measure(Size.Infinity); }
        ArrangePhases(labels.Max(label => label.DesiredSize.Width) * 3 + 16 > availableSize.Width);
        return base.MeasureOverride(availableSize);
    }
    private void ArrangePhases(bool vertical)
    {
        if (phases.RowDefinitions.Count == (vertical ? 3 : 2)) { return; }
        phases.RowDefinitions = new RowDefinitions(vertical ? "Auto,Auto,Auto" : "24,Auto");
        phases.ColumnDefinitions = new ColumnDefinitions(vertical ? "24,*" : "*,*,*");
        phases.ColumnSpacing = 8;
        track.Vertical = vertical; track.Width = vertical ? 24 : double.NaN; track.Height = vertical ? double.NaN : 24;
        Grid.SetRowSpan(track, vertical ? 3 : 1); Grid.SetColumnSpan(track, vertical ? 1 : 3);
        for (var index = 0; index < labels.Length; index++)
        {
            var label = labels[index];
            Grid.SetRow(label, vertical ? index : 1); Grid.SetColumn(label, vertical ? 1 : index);
            label.TextAlignment = vertical ? TextAlignment.Left : TextAlignment.Center;
            label.Margin = vertical ? new Thickness(0, 4) : default;
            label.MinHeight = vertical ? 24 : 0;
        }
        track.InvalidateVisual();
    }
    public void SetRequest(ActivityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (rendered is { } previous && previous.RequestId == request.RequestId && previous.Revision == request.Revision) { return; }
        rendered = request;
        identity.Text = $"Request {request.RequestId[..8]} · Room not reported";
        title.Text = request.State switch
        {
            RequestActivityState.Received => "Received", RequestActivityState.Working => "Working",
            RequestActivityState.NeedsInput => "Reply requested", RequestActivityState.ResponseReady => "Response ready",
            RequestActivityState.Failed => "Reported failure", RequestActivityState.Continued => "Later turn received", _ => "Outcome unknown"
        };
        title[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(request.State switch
        {
            RequestActivityState.NeedsInput => "AccentBrush", RequestActivityState.Failed => "ProblemBrush",
            RequestActivityState.ResponseReady => "SageBrush", RequestActivityState.OutcomeUnknown => "MutedBrush", _ => "InfoBrush"
        });
        track.State = request.State; track.InvalidateVisual();
        // These outcomes do not establish which processing phase was reached.
        phases.IsVisible = request.State is not (RequestActivityState.Continued or RequestActivityState.Failed or RequestActivityState.OutcomeUnknown);
        timing.Text = $"Reported {request.UpdatedAt.UtcDateTime:yyyy-MM-dd HH:mm:ss 'UTC'}";
        explanation.Text = request.State switch
        {
            RequestActivityState.NeedsInput => "A response requested input. Reply through your existing Lucia conversation.",
            RequestActivityState.Continued => "A later verified turn arrived, not necessarily an answer.",
            RequestActivityState.Failed => "No diagnostic text or physical outcome is inferred.",
            RequestActivityState.OutcomeUnknown => "Completion was not observed. Do not assume the request succeeded or failed.",
            _ => ""
        };
        if (request.PreviousRequestId is { } previousId) { explanation.Text += $" Previous request {previousId[..8]}."; }
        explanation.IsVisible = !string.IsNullOrWhiteSpace(explanation.Text);
        SetValue(AutomationProperties.NameProperty, $"Request {request.RequestId[..8]}. {title.Text}. Room not reported.");
    }
    private static TextBlock Small() => new() { Classes = { "muted" }, TextWrapping = TextWrapping.Wrap,
        [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13") };
}
