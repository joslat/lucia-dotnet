using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Core;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class BindingEditorTests
{
    [AvaloniaTheory]
    [InlineData(390, 844, false, 1.0)]
    [InlineData(390, 844, true, 1.0)]
    [InlineData(1194, 834, false, 1.0)]
    [InlineData(1194, 834, true, 1.0)]
    [InlineData(390, 844, false, 2.0)]
    [InlineData(390, 844, true, 2.0)]
    [InlineData(1194, 834, false, 2.0)]
    [InlineData(1194, 834, true, 2.0)]
    public void SourceRoleSelectionBindsOnlyTheSelectedPlaceOnSave(int width, int height, bool dark, double scale)
    {
        var theme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        var original = SampleHome.Create();
        var source = new ObservationSource("sensor.synthetic_humidity", "Synthetic humidity", [ObservationRole.Humidity]);
        var editor = new HomeEditorScreen(original, [source]);
        HomeTopology? saved = null;
        editor.SaveRequested += home => saved = home;
        var window = new Window { Width = width, Height = height, Content = editor };
        try
        {
            window.Show();
            CompanionTypography.Apply(size => size * scale);
            var picker = editor.FindControl<SourceBindingPicker>("BindingPicker")!;
            picker.FindControl<ComboBox>("BindingSource")!.SelectedIndex = 0;
            picker.FindControl<ComboBox>("BindingRole")!.SelectedIndex = 0;
            Click(picker.FindControl<Button>("AddBinding")!);
            Assert.Empty(original.Bindings);
            Assert.Single(picker.FindControl<StackPanel>("BindingRows")!.Children);
            editor.RequestBack();
            Assert.True(editor.FindControl<Border>("DiscardConfirmation")!.IsVisible);
            Click(editor.FindControl<Button>("StayInSetup")!);
            Click(editor.FindControl<Button>("SaveHome")!);
            var binding = Assert.Single(Assert.IsType<HomeTopology>(saved).Bindings);
            Assert.Equal("living", binding.PlaceId);
            Assert.Equal(ObservationRole.Humidity, binding.Role);
            Assert.Equal(source.Id, binding.SourceId);
            Dispatcher.UIThread.RunJobs();
            picker.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"bindings-{width}-{(dark ? "dark" : "light")}-{scale:0.0}.png");
            picker.FindControl<Button>("AddBinding")!.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, $"bindings-bottom-{width}-{(dark ? "dark" : "light")}-{scale:0.0}.png");
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); Application.Current.RequestedThemeVariant = theme; }
    }

    [AvaloniaFact]
    public void MissingDiscoveryKeepsExistingBindingAndSupportsExplicitRemoval()
    {
        var home = SampleHome.Create();
        var configured = new HomeTopology(home.Id, home.Name, home.Places, home.Connections,
            [new("living", "sensor.missing_source", ObservationRole.Temperature)]);
        var editor = new HomeEditorScreen(configured, []);
        HomeTopology? saved = null;
        editor.SaveRequested += result => saved = result;
        var window = new Window { Width = 1194, Height = 834, Content = editor };
        try
        {
            window.Show();
            var picker = editor.FindControl<SourceBindingPicker>("BindingPicker")!;
            Assert.False(picker.FindControl<Button>("AddBinding")!.IsEnabled);
            var rows = picker.FindControl<StackPanel>("BindingRows")!;
            var row = Assert.IsType<StackPanel>(Assert.Single(rows.Children));
            Assert.Contains(row.Children.OfType<TextBlock>(), text => text.Text!.Contains("not in current discovery"));
            Dispatcher.UIThread.RunJobs();
            picker.BringIntoView();
            Dispatcher.UIThread.RunJobs();
            NativeCapture.Save(window, "binding-missing-source.png");
            Click(row.Children.OfType<Button>().Single());
            Click(editor.FindControl<Button>("SaveHome")!);
            Assert.Empty(Assert.IsType<HomeTopology>(saved).Bindings);
            Assert.Single(configured.Bindings);
            NativeCapture.Save(window, "binding-editor-tablet.png");
        }
        finally { window.Close(); }
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateRoleReportsItsOwnerBesideThePickerAtLargeText(bool dark)
    {
        var theme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        var home = SampleHome.Create();
        var source = new ObservationSource("sensor.shared", "Sample temperature", [ObservationRole.Temperature]);
        var configured = new HomeTopology(home.Id, home.Name, home.Places, home.Connections,
            [new("garden", source.Id, ObservationRole.Temperature)]);
        var editor = new HomeEditorScreen(configured, [source]);
        HomeTopology? saved = null;
        editor.SaveRequested += result => saved = result;
        var window = new Window { Width = 390, Height = 844, Content = editor };
        try
        {
            window.Show();
            CompanionTypography.Apply(size => size * 2);
            var picker = editor.FindControl<SourceBindingPicker>("BindingPicker")!;
            picker.FindControl<ComboBox>("BindingSource")!.SelectedIndex = 0;
            picker.FindControl<ComboBox>("BindingRole")!.SelectedIndex = 0;
            Click(picker.FindControl<Button>("AddBinding")!);
            Dispatcher.UIThread.RunJobs();
            var error = picker.FindControl<TextBlock>("BindingError")!;
            Assert.True(error.IsVisible);
            Assert.Contains("Garden", error.Text);
            Assert.Contains("Remove that binding", error.Text);
            Assert.Equal(error, window.FocusManager!.GetFocusedElement());
            var bottom = error.TranslatePoint(new Point(0, error.Bounds.Height), window);
            Assert.NotNull(bottom);
            Assert.InRange(bottom.Value.Y, 0, window.Height);
            NativeCapture.Save(window, $"binding-duplicate-390-{(dark ? "dark" : "light")}-2.0.png");
            Click(editor.FindControl<Button>("SaveHome")!);
            Assert.Equal(configured.Bindings, Assert.IsType<HomeTopology>(saved).Bindings);
        }
        finally { CompanionTypography.Apply(size => size); window.Close(); Application.Current.RequestedThemeVariant = theme; }
    }

    [AvaloniaFact]
    public void NewlyBoundEmptyPlaceExplainsMissingReadingsBeforeAndAfterRestore()
    {
        var original = SampleHome.Create();
        var draft = new Lucia.Companion.App.ViewModels.HomeEditorViewModel(original);
        var id = draft.AddPlace("Greenhouse", PlaceIcon.Garden);
        draft.BindSource(id, new ObservationSource("sensor.new_soil", "Sample soil", [ObservationRole.Moisture]), ObservationRole.Moisture);
        var configured = draft.Build();
        var restored = HomeProfile.From(configured, []).ApplyTo(original);
        var detail = new PlaceDetail();
        var window = new Window { Width = 390, Height = 844, Content = detail };
        try
        {
            window.Show();
            foreach (var home in new[] { configured, restored })
            {
                var place = home.Places.Single(place => place.Id == id);
                Assert.Equal(PlaceState.Unavailable, place.State);
                Assert.Empty(place.Observations);
                Assert.DoesNotContain("No observation bindings", place.Reason);
                detail.SetPlace(place, home);
                Assert.Equal("Observation bindings configured. No readings available.", detail.FindControl<TextBlock>("EmptyText")!.Text);
            }
            NativeCapture.Save(window, "bound-empty-place.png");
        }
        finally { window.Close(); }
    }
}
