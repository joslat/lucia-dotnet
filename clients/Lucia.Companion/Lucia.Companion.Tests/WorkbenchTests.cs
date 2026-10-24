using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class WorkbenchTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void NativeWorkbenchRendersAndReflows(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var view = new ControlWorkbench();
        var window = new Window { Width = width, Height = height, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var orbs = view.GetVisualDescendants().OfType<PlaceOrb>().ToArray();
            Assert.Equal(6, orbs.Length);
            Assert.All(orbs, orb => Assert.InRange(orb.Bounds.Width, 100, width));
            Assert.All(orbs, orb => Assert.True(orb.Bounds.Height >= 48));
            Assert.True(orbs[0].IsSelected);
            Assert.NotEqual(orbs[0].Bounds.Position, orbs[1].Bounds.Position);
            Assert.Equal(width < 600, orbs[^1].Bounds.Y > orbs[0].Bounds.Y);
            Assert.All(orbs, orb => Assert.NotNull(ControlAutomationPeer.CreatePeerForElement(orb)));
            var themeButton = view.FindControl<Button>("LightTheme")!;
            Assert.Equal(16, themeButton.FontSize);
            Assert.Contains("DM Sans", themeButton.FontFamily.Name);

            NativeCapture.Save(window, $"workbench-{width}-{(dark ? "dark" : "light")}.png");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void KeyboardSelectionAndThemeChangesWork()
    {
        var view = new ControlWorkbench();
        var window = new Window { Width = 390, Height = 844, Content = view };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var orbs = view.GetVisualDescendants().OfType<PlaceOrb>().ToArray();
            orbs[1].Focus();
            window.KeyPress(Key.Enter, Avalonia.Input.RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, Avalonia.Input.RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(orbs[1].IsSelected);
            Assert.False(orbs[0].IsSelected);
            Assert.Equal("Garden", view.FindControl<TextBlock>("SelectedName")!.Text);
            Assert.Contains("Garden", orbs[1].GetValue(AutomationProperties.NameProperty));
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Unknown");

            view.FindControl<Button>("DarkTheme")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
            view.FindControl<Button>("LightTheme")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
            view.FindControl<Button>("SystemTheme")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(ThemeVariant.Default, Application.Current.RequestedThemeVariant);
        }
        finally
        {
            window.Close();
        }
    }
}
