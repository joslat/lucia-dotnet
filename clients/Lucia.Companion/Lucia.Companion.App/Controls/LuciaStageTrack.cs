using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lucia.Companion.Connection;

namespace Lucia.Companion.App.Controls;

/// <summary>Decorative phase connectors. Written state labels remain in LuciaStagePath.</summary>
internal sealed class LuciaStageTrack : Control
{
    public RequestActivityState State { get; set; }
    public bool Vertical { get; set; }
    public LuciaStageTrack() { Height = 24; IsHitTestVisible = false; ActualThemeVariantChanged += (_, _) => InvalidateVisual(); }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        IBrush Brush(string key) => (IBrush)this.FindResource(ActualThemeVariant, key)!;
        var points = Enumerable.Range(0, 3).Select(index => Vertical
            ? new Point(12, Bounds.Height * (index * 2 + 1) / 6)
            : new Point(Bounds.Width * (index * 2 + 1) / 6, 12)).ToArray();
        var completedProcessing = State is RequestActivityState.NeedsInput or RequestActivityState.ResponseReady;
        for (var index = 0; index < 2; index++)
        {
            var reported = index == 0 && (State == RequestActivityState.Working || completedProcessing) || index == 1 && completedProcessing;
            context.DrawLine(new Pen(Brush(reported ? "SageBrush" : "GraphLineBrush"), 1.5), points[index], points[index + 1]);
        }
        var current = State == RequestActivityState.Received ? 0 : State == RequestActivityState.Working ? 1 : 2;
        for (var index = 0; index < 3; index++)
        {
            var done = index == 0 && State != RequestActivityState.Received || index == 1 && completedProcessing
                || index == 2 && State == RequestActivityState.ResponseReady;
            var key = done ? "SageBrush" : index == current ? State switch
                { RequestActivityState.NeedsInput => "AccentBrush", RequestActivityState.Failed => "ProblemBrush", RequestActivityState.OutcomeUnknown => "MutedBrush", _ => "InfoBrush" } : "LineBrush";
            var point = points[index]; var pen = new Pen(Brush(key), index == current ? 2 : 1.5);
            context.DrawEllipse(Brush("PanelBrush"), pen, point, 8, 8);
            if (done)
            {
                context.DrawLine(pen, point + new Vector(-3, 0), point + new Vector(-1, 2));
                context.DrawLine(pen, point + new Vector(-1, 2), point + new Vector(3, -3));
            }
            else if (index == current && State == RequestActivityState.Failed)
            {
                context.DrawLine(pen, point + new Vector(-2.5, -2.5), point + new Vector(2.5, 2.5));
                context.DrawLine(pen, point + new Vector(-2.5, 2.5), point + new Vector(2.5, -2.5));
            }
            else if (index == current && State != RequestActivityState.OutcomeUnknown) { context.DrawEllipse(Brush(key), null, point, 3, 3); }
        }
    }
}
