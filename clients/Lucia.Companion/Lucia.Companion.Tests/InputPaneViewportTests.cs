using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class InputPaneViewportTests
{
    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(834, false)]
    [InlineData(834, true)]
    public void KeyboardKeepsSetupCommandsAndFocusedFieldAboveItsEdge(int width, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var editor = new HomeEditorScreen();
        var viewport = new InputPaneViewport { Child = editor };
        var window = new Window { Width = width, Height = 844, Content = viewport };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var input = editor.FindControl<TextBox>("PlaceNameInput")!;
            input.Focus(); Dispatcher.UIThread.RunJobs();
            viewport.SetOcclusion(new Rect(0, 544, width, 300)); Dispatcher.UIThread.RunJobs();
            foreach (var control in new Control[] { input, editor.FindControl<Button>("SaveHome")!, editor.FindControl<Button>("CancelEdit")! })
            {
                var top = control.TranslatePoint(default, window)!.Value.Y;
                Assert.InRange(top, 0, 544 - control.Bounds.Height);
            }
            Assert.Equal(300, viewport.Padding.Bottom);
            viewport.SetOcclusion(default); Dispatcher.UIThread.RunJobs();
            Assert.Equal(default, viewport.Padding);
            Assert.Equal(844, editor.Bounds.Height);
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); }
    }

    [AvaloniaFact]
    public void ExistingSafeAreaIsNotCountedAgainAndDetachClearsTheInset()
    {
        var viewport = new InputPaneViewport { Child = new Border() };
        var safeArea = new Border { Padding = new Thickness(0, 24, 0, 24), Child = viewport };
        var window = new Window { Width = 390, Height = 844, Content = safeArea };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            viewport.SetOcclusion(new Rect(0, 544, 390, 276)); Dispatcher.UIThread.RunJobs();
            Assert.Equal(276, viewport.Padding.Bottom);
            safeArea.Child = null;
            Assert.Equal(default, viewport.Padding);
            safeArea.Child = viewport; Dispatcher.UIThread.RunJobs();
            Assert.Equal(default, viewport.Padding);
            viewport.SetOcclusion(new Rect(0, 400, 390, 420)); Dispatcher.UIThread.RunJobs();
            Assert.Equal(420, viewport.Padding.Bottom);
            viewport.SetOcclusion(default); Dispatcher.UIThread.RunJobs();
            Assert.Equal(default, viewport.Padding);
        }
        finally { window.Close(); }
    }
}
