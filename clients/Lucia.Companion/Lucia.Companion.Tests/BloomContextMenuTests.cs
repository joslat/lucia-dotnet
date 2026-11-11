using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.ViewModels;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class BloomContextMenuTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeContextRequestsCannotStackAMenuOnAPendingOrActiveBloomHold(bool active)
    {
        var constellation = new HomeConstellation { HoldDelay = active ? TimeSpan.Zero : TimeSpan.FromSeconds(10) };
        constellation.SetHome(SampleHome.Create());
        var window = new Window { Width = 1194, Height = 834, Content = constellation };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var orb = constellation.Orbs[0];
            var circle = orb.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "Orb");
            var center = circle.TranslatePoint(new Point(circle.Bounds.Width / 2, circle.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            orb.RaiseEvent(new ContextRequestedEventArgs()); Dispatcher.UIThread.RunJobs();
            Assert.False(orb.ContextMenu!.IsOpen);
            window.MouseUp(center, MouseButton.Left); Dispatcher.UIThread.RunJobs();
            orb.RaiseEvent(new ContextRequestedEventArgs()); Dispatcher.UIThread.RunJobs();
            Assert.True(orb.ContextMenu.IsOpen);
            orb.ContextMenu.Close();
        }
        finally { window.Close(); }
    }
}
