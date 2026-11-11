using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class CompactRecoveryNoticeTests
{
    [AvaloniaTheory]
    [InlineData("retry")]
    [InlineData("conflict")]
    [InlineData("unreadable")]
    public async Task PersistentRecoveryLeavesRoomForTheTaskAndOffersCompleteNamedDetails(string kind)
    {
        var directory = Directory.CreateTempSubdirectory("lucia-recovery-notice-");
        var file = Path.Combine(directory.FullName, "home.json");
        if (kind == "unreadable") { await File.WriteAllTextAsync(file, "unreadable fixture", TestContext.Current.CancellationToken); }
        CompanionTypography.Apply(size => size * 2);
        var state = new HomeViewModel(SampleHome.Create());
        var store = new LocalHomeStore(file);
        var shell = new CompanionShell(state, store);
        var window = new Window { Width = 390, Height = 500, Content = shell };
        try
        {
            window.Show(); await shell.ProfileSession.InitializeAsync(TestContext.Current.CancellationToken);
            if (kind != "unreadable")
            {
                if (kind == "retry") { Directory.CreateDirectory(file); }
                else { await store.SaveAsync(HomeProfile.From(state.Home, []), null, TestContext.Current.CancellationToken); }
                state.SelectPlace("garden"); state.CloseDetail(); state.TogglePin(); await shell.ProfileSession.PendingSave;
            }
            Dispatcher.UIThread.RunJobs();
            var footer = shell.FindControl<StackPanel>("StorageFooter")!;
            NativeCapture.Save(window, $"recovery-compact-{kind}.png");
            var usable = 500 - shell.Navigation.Bounds.Height;
            Assert.True(shell.FindControl<ContentControl>("ScreenHost")!.Bounds.Height > usable / 2,
                $"The collapsed recovery notice uses {footer.Bounds.Height} of {usable} usable units.");
            var details = shell.FindControl<ToggleButton>("StorageDetailsToggle");
            Assert.NotNull(details);
            Assert.True(details.IsEffectivelyVisible);
            foreach (var button in shell.FindControl<Panel>("StorageActions")!.Children.OfType<Button>().Where(item => item.IsEffectivelyVisible))
            {
                AssertLabelFits(button);
            }
            Assert.False(shell.FindControl<ScrollViewer>("StorageDetailsScroll")!.IsVisible);
            Assert.True(details.Focus());
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Dispatcher.UIThread.RunJobs();
            Assert.True(shell.FindControl<ScrollViewer>("StorageDetailsScroll")!.IsEffectivelyVisible);
            Assert.Equal(shell.ProfileSession.StatusText, shell.FindControl<TextBlock>("StorageDetailsText")!.Text);
            NativeCapture.Save(window, $"recovery-details-{kind}.png");
            foreach (var button in shell.FindControl<Panel>("StorageActions")!.Children.OfType<Button>().Where(item => item.IsEffectivelyVisible))
            {
                AssertLabelFits(button);
            }
            var back = new RoutedEventArgs(TopLevel.BackRequestedEvent); window.RaiseEvent(back); Dispatcher.UIThread.RunJobs();
            Assert.True(back.Handled);
            Assert.False(shell.FindControl<ScrollViewer>("StorageDetailsScroll")!.IsVisible);
            Assert.Same(details, window.FocusManager!.GetFocusedElement());
            if (kind == "unreadable")
            {
                shell.FindControl<Button>("ReviewLocalSetup")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                var editor = Assert.IsType<HomeEditorScreen>(shell.CurrentScreen);
                Assert.True(editor.FindControl<TextBlock>("ReplacementWarning")!.IsVisible);
                Assert.Equal("unreadable fixture", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
            }
        }
        finally { window.Close(); await shell.ProfileSession.PendingSave; CompanionTypography.Apply(size => size); directory.Delete(recursive: true); }
    }

    private static void AssertLabelFits(Button button)
    {
        var label = Assert.Single(button.GetVisualDescendants().OfType<TextBlock>());
        var natural = new TextBlock { Text = label.Text, FontFamily = label.FontFamily, FontSize = label.FontSize, FontWeight = label.FontWeight };
        natural.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.True(label.Bounds.Width + .1 >= natural.DesiredSize.Width,
            $"{label.Text} needs {natural.DesiredSize.Width} units, but has {label.Bounds.Width}.");
    }
}
