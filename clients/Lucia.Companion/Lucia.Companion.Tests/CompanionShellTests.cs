using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompanionShellTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void WholeShellCanEditCancelAndReturnFromLab(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"app-{width}-{(dark ? "dark" : "light")}.png");
            var home = Assert.IsType<HomeScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            if (width >= 900)
            {
                var activity = home.GetVisualDescendants().OfType<Lucia.Companion.App.Controls.LuciaNow>().Single();
                var bottom = activity.TranslatePoint(new Point(0, activity.Bounds.Height), window);
                Assert.NotNull(bottom);
                Assert.InRange(bottom.Value.Y, 0, height - 54);
            }
            home.FindControl<ScrollViewer>("HomeScroll")!.Offset = new Vector(0, 380);
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"app-lower-{width}-{(dark ? "dark" : "light")}.png");
            home.FindControl<ScrollViewer>("HomeScroll")!.Offset = Vector.Zero;
            state.SelectPlace("porch");
            state.OpenActions();
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"app-bloom-{width}-{(dark ? "dark" : "light")}.png");
            state.CloseActions();
            state.CloseDetail();
            shell.FindControl<Button>("BuildHomeButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var editor = Assert.IsType<HomeEditorScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            editor.FindControl<TextBox>("PlaceNameInput")!.Text = "Not saved";
            NativeCapture.Save(window, $"setup-{width}-{(dark ? "dark" : "light")}.png");
            editor.FindControl<Button>("CancelEdit")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal("Living room", state.Home.Places[0].Name);
            shell.FindControl<Button>("LabButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsType<ControlWorkbench>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
            shell.FindControl<Button>("LabButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.IsType<HomeScreen>(shell.FindControl<ContentControl>("ScreenHost")!.Content);
        }
        finally { window.Close(); }
    }
}
