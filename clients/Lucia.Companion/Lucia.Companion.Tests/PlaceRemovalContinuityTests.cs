using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class PlaceRemovalContinuityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplacementKeepsAnOpenPageOnlyWhileItsPlaceExists(bool remove)
    {
        var original = SampleHome.Create();
        var state = new HomeViewModel(original);
        state.SelectPlace("garden"); state.OpenPage();
        var draft = new HomeEditorViewModel(original);
        if (remove) { draft.RemovePlace("garden"); }
        state.ReplaceHome(draft.Build());
        Assert.Equal(!remove, state.IsPageOpen);
        Assert.Equal(!remove, state.IsDetailOpen);
        Assert.Equal(remove, state.SelectedPlace is null);
    }

    [AvaloniaTheory]
    [InlineData(390, true)]
    [InlineData(1194, true)]
    [InlineData(390, false)]
    [InlineData(1194, false)]
    public void SavingSetupCannotLeaveADeletedPlacePageVisible(int width, bool remove)
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = 834, Content = shell };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            state.SelectPlace("garden"); state.OpenPage(); Dispatcher.UIThread.RunJobs();
            Assert.True(home.IsPageVisible);
            Press(shell.Navigation.ButtonFor(ShellDestination.More));
            Press(Assert.IsType<MoreScreen>(shell.CurrentScreen).FindControl<Button>("OpenHomeSetup")!);
            var editor = Assert.IsType<HomeEditorScreen>(shell.CurrentScreen);
            if (remove)
            {
                var list = editor.FindControl<ListBox>("PlacesList")!;
                list.SelectedItem = list.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "garden"));
                Press(editor.FindControl<Button>("RemovePlace")!);
                Press(editor.FindControl<Button>("ConfirmRemove")!);
            }
            Press(editor.FindControl<Button>("SaveHome")!);
            Assert.Same(home, shell.CurrentScreen);
            Assert.Equal(!remove, state.IsPageOpen);
            Assert.Equal(!remove, home.IsPageVisible);
            Assert.Equal(!remove, home.FindControl<PlacePage>("PlacePage")!.IsEffectivelyVisible);
            Assert.Equal(remove, state.SelectedPlace is null);
            NativeCapture.Save(window, $"place-after-setup-{width}-{remove}.png");
        }
        finally { window.Close(); }

        void Press(Button button)
        {
            button.BringIntoView(); Dispatcher.UIThread.RunJobs(); Assert.True(button.Focus());
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
