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
    private readonly TextBlock topic = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock title = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock identity = Small();
    private readonly TextBlock targets = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock targetCoverage = Small();
    private readonly TextBlock timing = Small();
    private readonly TextBlock explanation = Small();
    private readonly LuciaStageTrack track = new();
    private readonly Grid phases = new();
    private readonly TextBlock[] labels = [Small(), Small(), Small()];
    private ActivityRequest? rendered;
    public LuciaStagePath()
    {
        var content = new StackPanel { Spacing = 5 };
        content.Children.Add(topic); content.Children.Add(title); content.Children.Add(targets); content.Children.Add(targetCoverage);
        phases.Children.Add(track);
        foreach (var (label, index) in new[] { ("Received", 0), ("Processing", 1), ("Response", 2) })
        {
            var text = labels[index]; text.Text = label; text.TextWrapping = TextWrapping.NoWrap;
            text.VerticalAlignment = VerticalAlignment.Center; phases.Children.Add(text);
        }
        ArrangePhases(false);
        content.Children.Add(phases); content.Children.Add(timing); content.Children.Add(explanation); content.Children.Add(identity);
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
        if (rendered is { } previous && previous.RequestId == request.RequestId && previous.Revision == request.Revision
            && SameAreas(previous.CurrentTargetAreas, request.CurrentTargetAreas)) { return; }
        rendered = request;
        identity.Text = $"Request {request.RequestId[..8]}";
        topic.Text = request.Category switch
        {
            RequestActivityCategory.Lighting => "Lighting request", RequestActivityCategory.Climate => "Climate request",
            RequestActivityCategory.Scene => "Scene request", _ => "Lucia request"
        };
        targets.Text = request.CurrentTargetAreas is { } areas ? $"Current target areas: {string.Join(", ", areas.Places.Select(place => place.Name))}" : "Target area not reported";
        targetCoverage.Text = request.CurrentTargetAreas?.IsPartial == true ? "Some target areas cannot be shown." : "";
        targetCoverage.IsVisible = targetCoverage.Text.Length > 0;
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
        SetValue(AutomationProperties.NameProperty, $"{topic.Text}. {title.Text}. {targets.Text}. {targetCoverage.Text} Request {request.RequestId[..8]}.");
        SetValue(AutomationProperties.HelpTextProperty, request.CurrentTargetAreas is { } checkedAreas
            ? $"Current registry assignment, not origin or physical result. Registry checked {checkedAreas.CheckedAt.UtcDateTime:yyyy-MM-dd HH:mm:ss 'UTC'}." : "No qualified target area is available.");
    }
    private static bool SameAreas(ActivityTargetAreas? first, ActivityTargetAreas? second) => ReferenceEquals(first, second)
        || first is not null && second is not null && first.CheckedAt == second.CheckedAt && first.IsPartial == second.IsPartial
            && first.Places.Select(place => (place.PlaceId, place.Name)).SequenceEqual(second.Places.Select(place => (place.PlaceId, place.Name)));
    private static TextBlock Small() => new() { Classes = { "muted" }, TextWrapping = TextWrapping.Wrap,
        [!TextBlock.FontSizeProperty] = new DynamicResourceExtension("TextSize13") };
}
