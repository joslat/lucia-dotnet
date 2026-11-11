using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class NativeAccentTests
{
    [AvaloniaFact]
    public void DayAndWeekChoicesHaveAtLeast48LogicalUnitsOfTouchHeight()
    {
        var screen = new StoryScreen(SampleStory.Navigator(SampleHome.Create()));
        var window = new Window { Width = 390, Height = 844, Content = screen };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.True(screen.FindControl<RadioButton>("DayMode")!.Bounds.Height >= 48);
            Assert.True(screen.FindControl<RadioButton>("WeekMode")!.Bounds.Height >= 48);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeAppearanceChoicesUseTheSameAccentAsCustomActions(bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var screen = new MoreScreen();
        var window = new Window { Width = 390, Height = 844, Content = screen };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var radio = screen.FindControl<RadioButton>(dark ? "ThemeDark" : "ThemeLight")!;
            Assert.True(radio.IsChecked);
            Assert.True(Application.Current.Resources.TryGetResource("AccentBrush", dark ? ThemeVariant.Dark : ThemeVariant.Light, out var value));
            var accent = Assert.IsAssignableFrom<ISolidColorBrush>(value);
            Assert.Contains(radio.GetVisualDescendants(), visual => visual is Shape { Fill: ISolidColorBrush fill } && fill.Color == accent.Color
                || visual is Border { Background: ISolidColorBrush background } && background.Color == accent.Color);
            NativeCapture.Save(window, $"quality-native-accent-{(dark ? "dark" : "light")}.png");
        }
        finally { window.Close(); }
    }
}
