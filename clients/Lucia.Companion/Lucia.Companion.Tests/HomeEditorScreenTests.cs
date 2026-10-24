using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class HomeEditorScreenTests
{
    [AvaloniaFact]
    public void AddRoomConnectAndSaveIsOneDraftTransaction()
    {
        var original = SampleHome.Create();
        var editor = new HomeEditorScreen(original);
        HomeTopology? saved = null;
        editor.SaveRequested += home => saved = home;
        var window = new Window { Width = 390, Height = 844, Content = editor };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            editor.FindControl<Button>("AddPlace")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            editor.FindControl<TextBox>("PlaceNameInput")!.Text = "Greenhouse";
            var connections = editor.FindControl<StackPanel>("ConnectionsList")!;
            connections.Children.OfType<CheckBox>().Single(check => Equals(check.Tag, "garden")).IsChecked = true;
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.NotNull(saved);
            Assert.Equal(7, saved.Places.Count);
            var added = saved.Places.Single(place => place.Name == "Greenhouse");
            Assert.Contains(saved.Connections, edge => edge.FromPlaceId == added.Id && edge.ToPlaceId == "garden");
            Assert.Equal(6, original.Places.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void BlankNameCannotSaveAndCancelDoesNotMutateOriginal()
    {
        var original = SampleHome.Create();
        var editor = new HomeEditorScreen(original);
        var saves = 0;
        var cancels = 0;
        editor.SaveRequested += _ => saves++;
        editor.CancelRequested += () => cancels++;
        var window = new Window { Width = 1194, Height = 834, Content = editor };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            editor.FindControl<TextBox>("PlaceNameInput")!.Text = " ";
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(0, saves);
            Assert.True(editor.FindControl<TextBlock>("ErrorText")!.IsVisible);
            editor.FindControl<Button>("CancelEdit")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, cancels);
            Assert.Equal("Living room", original.Places[0].Name);
        }
        finally { window.Close(); }
    }
}
