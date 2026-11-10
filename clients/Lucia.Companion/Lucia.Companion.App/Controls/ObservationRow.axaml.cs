using Avalonia;
using Avalonia.Automation;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Lucia.Companion.Core;

namespace Lucia.Companion.App.Controls;

public sealed partial class ObservationRow : UserControl
{
    public static readonly StyledProperty<double> EmphasisOpacityProperty = AvaloniaProperty.Register<ObservationRow, double>(nameof(EmphasisOpacity));
    public double EmphasisOpacity { get => GetValue(EmphasisOpacityProperty); private set => SetValue(EmphasisOpacityProperty, value); }
    public Observation? Reading { get; private set; }
    private bool attached;
    private int emphasisVersion;

    public ObservationRow()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<Border>("ChangeEmphasis")!.Bind(OpacityProperty, this.GetObservable(EmphasisOpacityProperty));
        AttachedToVisualTree += (_, _) => { attached = true; MotionSettings.Current.Changed += MotionChanged; };
        DetachedFromVisualTree += (_, _) => { attached = false; MotionSettings.Current.Changed -= MotionChanged; StopEmphasis(); };
        _ = new PresentationVisibility(this, MotionChanged);
    }

    public ObservationRow(Observation observation) : this() => Update(observation);

    public void Update(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var previous = Reading;
        var sameIdentity = previous?.Label == observation.Label && previous.Role == observation.Role;
        var changed = sameIdentity && (previous!.Value != observation.Value || previous.Quality != observation.Quality);
        Reading = observation;
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
        if (!sameIdentity) { StopEmphasis(); }
        else if (changed && attached && IsEffectivelyVisible && MotionSettings.Current.IsEnabled) { Emphasize(); }
    }

    private void Emphasize()
    {
        var version = ++emphasisVersion;
        Transitions = null;
        EmphasisOpacity = .7;
        Transitions = [new DoubleTransition { Property = EmphasisOpacityProperty, Duration = TimeSpan.FromMilliseconds(360), Easing = new CubicEaseOut() }];
        Dispatcher.UIThread.Post(() => { if (version == emphasisVersion && attached && IsEffectivelyVisible && MotionSettings.Current.IsEnabled) { EmphasisOpacity = 0; } }, DispatcherPriority.Render);
    }
    private void StopEmphasis() { emphasisVersion++; Transitions = null; EmphasisOpacity = 0; }
    private void MotionChanged() { if (!attached || !IsEffectivelyVisible || !MotionSettings.Current.IsEnabled) { StopEmphasis(); } }
}
