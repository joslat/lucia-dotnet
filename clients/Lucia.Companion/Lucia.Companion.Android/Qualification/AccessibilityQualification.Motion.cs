using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;

namespace Lucia.Companion.Android.Qualification;

public sealed partial class AccessibilityQualification
{
    private readonly List<object> motionSamples = [];
    private readonly List<object> motionMarkers = [];
    private readonly Stopwatch motionClock = new();

    /// <summary>Actual installed app. The external runner supplies Android Home/resume and system-policy changes.</summary>
    private void QualifyMotion()
    {
        motionClock.Start();
        ActivateNow("NavMore"); SampleMotion("more-entry", 550);
        ActivateNow("NavStory"); SampleMotion("story-entry", 550);
        ActivateNow("WeekMode"); SampleMotion("week-entry", 450);
        ActivateNow("DayMode"); SampleMotion("day-entry", 450);
        ActivateNow("NavMore"); SampleMotion("rapid-more", 60);
        ActivateNow("NavStory"); SampleMotion("rapid-story", 60);
        ActivateNow("NavHome"); SampleMotion("rapid-home", 450);
        ActivateNow("NavMore"); SampleMotion("await-background", 1700);
        SampleMotion("await-resume", 1700);
        ActivateNow("NavStory"); SampleMotion("await-system-reduction", 1700);
        SampleMotion("await-system-restore", 1700);
        ActivateNow("NavHome"); SampleMotion("final-home", 500);
        using (var place = WaitFor(node => node.Clickable && node.ClassName == "PlaceOrb" && node.Text?.StartsWith("Living room", StringComparison.Ordinal) == true))
        using (var bounds = new global::Android.Graphics.Rect())
        {
            place.GetBoundsInScreen(bounds);
            SampleMotion("await-hold", 3200, new { x = bounds.CenterX(), y = bounds.CenterY() });
        }
        SetMotionPhase("complete");
    }

    private void ActivateNow(string id)
    {
        stage = "motion-locate-" + id;
        using var node = WaitFor(item => IsId(item, id) && item.Enabled);
        if (!node.PerformAction(global::Android.Views.Accessibility.Action.Click))
        { throw new InvalidOperationException("Native action rejected: " + id); }
    }

    private void SetMotionPhase(string phase, object? target = null)
    {
        stage = phase;
        motionMarkers.Add(new { phase, milliseconds = motionClock.Elapsed.TotalMilliseconds });
        var path = Path.Combine(TargetContext!.FilesDir!.AbsolutePath, "motion-phase.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new { phase, target }));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    private void SampleMotion(string phase, int duration, object? target = null)
    {
        SetMotionPhase(phase, target);
        var until = motionClock.ElapsedMilliseconds + duration;
        do
        {
            RunOnMainSync(() =>
            {
                var app = (Lucia.Companion.App.App)global::Avalonia.Application.Current!;
                var shell = app.CreateActivityView().GetVisualDescendants().OfType<CompanionShell>().Single();
                var screen = shell.CurrentScreen as Control ?? throw new InvalidOperationException("No native current screen.");
                var panels = screen.GetVisualDescendants().OfType<Control>().Prepend(screen)
                    .Where(control => PanelArrival.GetOrder(control) >= 0)
                    .Select(control => new { control.Name, visible = control.IsEffectivelyVisible,
                        opacity = control.Opacity, y = control.RenderTransform?.Value.M32 ?? 0 }).ToArray();
                var motion = MotionSettings.Current;
                var home = screen as Lucia.Companion.App.Screens.HomeScreen;
                var charges = home?.FindControl<HomeConstellation>("Constellation")?.Orbs.Select(orb => orb.HoldProgress).ToArray() ?? [];
                motionSamples.Add(new { phase, milliseconds = motionClock.Elapsed.TotalMilliseconds,
                    screen = screen.GetType().Name, screen.IsEffectivelyVisible,
                    hostOpacity = screen.GetVisualAncestors().OfType<ContentPresenter>().FirstOrDefault()?.Opacity ?? 1,
                    motion.IsEnabled, motion.IsPaused, motion.ReduceMotion, motion.SystemReduceMotion, panels,
                    charges, bloomOpen = home?.State.IsActionsOpen ?? false });
            });
            Thread.Sleep(16);
        } while (motionClock.ElapsedMilliseconds < until);
    }
}
