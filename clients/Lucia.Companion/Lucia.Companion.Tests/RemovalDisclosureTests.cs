using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class RemovalDisclosureTests
{
    [AvaloniaTheory]
    [InlineData(390, 620, false)]
    [InlineData(390, 620, true)]
    [InlineData(1194, 800, false)]
    [InlineData(1194, 800, true)]
    public void OpeningAtTheViewportBottomRevealsNamedQuestionAndBothChoices(int width, int height, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var original = SampleHome.Create();
        var editor = new HomeEditorScreen(original);
        var saves = 0; editor.SaveRequested += _ => saves++;
        var window = new Window { Width = width, Height = height, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var remove = editor.FindControl<Button>("RemovePlace")!;
            var scroll = editor.FindControl<ScrollViewer>("EditorScroll")!;
            remove.BringIntoView(); Dispatcher.UIThread.RunJobs();
            // Position the initiating action at the bottom. Do not reveal any newly opened choice from the test.
            var content = Assert.IsAssignableFrom<Visual>(scroll.Content);
            var top = remove.TranslatePoint(default, content)!.Value.Y;
            scroll.Offset = new Vector(0, Math.Max(0, top + remove.Bounds.Height - scroll.Viewport.Height));
            Dispatcher.UIThread.RunJobs(); Assert.True(remove.Focus()); Activate(window);
            var keep = editor.FindControl<Button>("KeepPlace")!;
            Assert.Same(keep, window.FocusManager!.GetFocusedElement());
            AssertInside(editor.FindControl<TextBlock>("RemovalQuestion")!, scroll);
            AssertInside(keep, scroll); AssertInside(editor.FindControl<Button>("ConfirmRemove")!, scroll);
            Assert.Contains("Living room", editor.FindControl<TextBlock>("RemovalQuestion")!.Text);
            Assert.Equal(0, saves);
            NativeCapture.Save(window, $"removal-revealed-{width}-{large}.png");
            Activate(window);
            Assert.False(editor.FindControl<StackPanel>("RemovalConfirmation")!.IsVisible);
            Assert.Same(remove, window.FocusManager.GetFocusedElement()); AssertInside(remove, scroll);
            Activate(window); editor.RequestBack(); Dispatcher.UIThread.RunJobs();
            Assert.Same(remove, window.FocusManager.GetFocusedElement()); AssertInside(remove, scroll);
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, saves); Assert.Equal(6, original.Places.Count);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(1194, false)]
    [InlineData(390, true)]
    [InlineData(1194, true)]
    public void ConfirmReturnsToAVisibleSelectorOrAddPlaceAndOnlyChangesTheDraft(int width, bool lastPlace)
    {
        var source = SampleHome.Create();
        var home = lastPlace ? new HomeTopology(source.Id, source.Name, [source.Places[0]], [], []) : source;
        var editor = new HomeEditorScreen(home);
        HomeTopology? saved = null; editor.SaveRequested += value => saved = value;
        var window = new Window { Width = width, Height = 650, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var remove = editor.FindControl<Button>("RemovePlace")!;
            remove.BringIntoView(); Dispatcher.UIThread.RunJobs(); remove.Focus(); Activate(window);
            var confirm = editor.FindControl<Button>("ConfirmRemove")!;
            confirm.Focus(); Activate(window);
            var target = window.FocusManager!.GetFocusedElement() as Control;
            Assert.NotNull(target); AssertInside(target, editor.FindControl<ScrollViewer>("EditorScroll")!);
            if (lastPlace) { Assert.Same(editor.FindControl<Button>("AddPlace"), target); }
            else if (width < 800) { Assert.Same(editor.FindControl<ComboBox>("PlaceChooser"), target); }
            else { Assert.IsType<ListBoxItem>(target); }
            Assert.Null(saved); Assert.Equal(lastPlace ? 1 : 6, home.Places.Count);
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.NotNull(saved); Assert.Equal(home.Places.Count - 1, saved.Places.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData("focus")]
    [InlineData("selection")]
    [InlineData("dismiss")]
    [InlineData("hide")]
    [InlineData("detach")]
    public void AStaleRevealCannotTakeFocusAfterTheUserChangesContext(string change)
    {
        var editor = new HomeEditorScreen(SampleHome.Create());
        var outside = new Button { Content = "Elsewhere" };
        var parent = new StackPanel { Children = { editor, outside } };
        var window = new Window { Width = 1194, Height = 800, Content = parent };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var remove = editor.FindControl<Button>("RemovePlace")!;
            remove.Focus(); remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (change=="selection") { editor.FindControl<ListBox>("PlacesList")!.SelectedIndex = 1; }
            if (change=="dismiss") { editor.RequestBack(); }
            if (change=="hide") { editor.IsVisible = false; }
            if (change=="detach") { parent.Children.Remove(editor); }
            Assert.True(outside.Focus()); Dispatcher.UIThread.RunJobs();
            Assert.Same(outside, window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); }
    }

    private static void Activate(Window window)
    {
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertInside(Control target, ScrollViewer scroll)
    {
        Assert.True(target.IsEffectivelyVisible);
        var point = target.TranslatePoint(default, scroll)!.Value;
        Assert.True(point.Y >= -.1 && point.Y + target.Bounds.Height <= scroll.Viewport.Height + .1,
            $"{target.Name ?? target.GetType().Name} is at {point.Y}, height {target.Bounds.Height}, viewport {scroll.Viewport.Height}.");
    }
}
