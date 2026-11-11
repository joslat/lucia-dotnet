using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class AttentionReadabilityTests
{
    [AvaloniaTheory]
    [InlineData(390, false, false)]
    [InlineData(390, false, true)]
    [InlineData(390, true, false)]
    [InlineData(390, true, true)]
    [InlineData(1194, false, false)]
    [InlineData(1194, false, true)]
    [InlineData(1194, true, false)]
    [InlineData(1194, true, true)]
    public void ProblemWaitingAndUnavailableReasonsRemainCompleteAcrossTextAndWidthChanges(int width, bool large, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        CompanionTypography.Apply(size => size * (large ? 2 : 1));
        var state = new HomeViewModel(SampleHome.Create());
        var home = new HomeScreen(state);
        var window = new Window { Width = width, Height = 844, Content = home };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var attention = home.FindControl<AttentionList>("Attention")!;
            var rows = attention.Rows.ToArray();
            Assert.Equal(3, rows.Length);
            foreach (var row in rows)
            {
                var place = state.Home.Places.Single(place => place.Id == (string)row.Tag!);
                var reason = row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == place.Reason);
                Assert.NotEmpty(reason.TextLayout.TextLines);
                Assert.DoesNotContain(reason.TextLayout.TextLines, line => line.HasCollapsed);
                Assert.True(reason.TextLayout.Height <= reason.Bounds.Height + .5);
                var origin = reason.TranslatePoint(default, row)!.Value;
                Assert.True(origin.Y >= 0 && origin.Y + reason.Bounds.Height <= row.Bounds.Height + .5);
            }
            rows[0].BringIntoView(); Dispatcher.UIThread.RunJobs();
            Assert.True(rows[0].Focus());
            Assert.Same(rows[0], window.FocusManager!.GetFocusedElement());
            NativeCapture.Save(window, $"attention-complete-{width}-{large}-{dark}.png");
            window.Width = width == 390 ? 1194 : 390;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(rows, attention.Rows);
            Assert.Same(rows[0], window.FocusManager!.GetFocusedElement());
            rows[0].BringIntoView(); Dispatcher.UIThread.RunJobs();
            var point = rows[0].TranslatePoint(new Point(rows[0].Bounds.Width / 2, rows[0].Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Assert.Equal("utility", state.SelectedPlace?.Id);
            Assert.True(state.IsDetailOpen);
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }
}
