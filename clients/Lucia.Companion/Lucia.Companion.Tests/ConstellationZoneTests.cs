using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.App.ViewModels;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class ConstellationZoneTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void DemoPlacesAreGroupedByZoneAndCaptionedWithTheirReadings(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = width, Height = height, Content = shell };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var home = Assert.IsType<HomeScreen>(shell.CurrentScreen);
            var constellation = home.FindControl<HomeConstellation>("Constellation")!;
            Assert.Equal(["Inside", "Outside", "Own building"], constellation.ZoneLabels.Select(label => label.Text));
            Assert.Equal(["living", "kitchen", "bedroom", "garden", "porch", "utility"], constellation.Orbs.Select(orb => (string)orb.Tag!));
            var orbs = constellation.Orbs.ToDictionary(orb => (string)orb.Tag!);
            Assert.Equal("Lights off · 21.4 °C", orbs["living"].Caption);
            Assert.Null(orbs["living"].StatusText);
            // One line under each place: the status when there is one, otherwise the reading summary.
            Assert.Null(orbs["porch"].Caption);
            Assert.Equal("Waiting for you", orbs["porch"].StatusText);
            Assert.Equal("Changed · Gate open", orbs["garden"].StatusText);
            Assert.Null(orbs["bedroom"].Caption);
            Assert.Equal("No data since 10:00 UTC", orbs["bedroom"].StatusText);
            Assert.Equal("Lucia is checking", orbs["kitchen"].StatusText);
            Assert.Equal("Porch. Front door closed. Waiting for you. A sample request is waiting for a response.", orbs["porch"].GetValue(AutomationProperties.NameProperty));
            foreach (var label in constellation.ZoneLabels)
            {
                var first = constellation.Orbs.First(orb => PlaceZones.Label(state.Home.Places.Single(place => place.Id == (string)orb.Tag!).Zone) == label.Text);
                Assert.True(label.Bounds.Bottom <= first.Bounds.Top + 1, $"{label.Text} sits above its first place");
            }
            NativeCapture.Save(window, $"zones-{width}-{(dark ? "dark" : "light")}.png");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AHomeWithoutZonesKeepsItsOrderAndShowsNoZoneLabels()
    {
        var home = SampleHome.Create();
        var unzoned = new HomeTopology(home.Id, home.Name, home.Places.Select(place => place with { Zone = null }), home.Connections);
        var view = new HomeScreen(new HomeViewModel(unzoned));
        var window = new Window { Width = 390, Height = 844, Content = view };
        try
        {
            window.Show();
            var constellation = view.FindControl<HomeConstellation>("Constellation")!;
            Assert.Empty(constellation.ZoneLabels);
            Assert.Equal(unzoned.Places.Select(place => place.Id), constellation.Orbs.Select(orb => (string)orb.Tag!));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SetupChoosesAZoneThatSaveApplies()
    {
        var state = new HomeViewModel(SampleHome.Create());
        var shell = new CompanionShell(state, store: null);
        var window = new Window { Width = 1194, Height = 834, Content = shell };
        try
        {
            window.Show();
            ShellNavigationTests.OpenSetup(shell);
            var editor = Assert.IsType<HomeEditorScreen>(shell.CurrentScreen);
            var zone = editor.FindControl<ComboBox>("ZoneInput")!;
            Assert.Equal(PlaceZones.Choices, zone.ItemsSource);
            Assert.Equal("Inside", zone.SelectedItem);
            zone.SelectedItem = "Outside";
            editor.FindControl<Button>("SaveHome")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(PlaceZone.Outside, state.Home.Places.Single(place => place.Id == "living").Zone);
        }
        finally { window.Close(); }
    }
}
