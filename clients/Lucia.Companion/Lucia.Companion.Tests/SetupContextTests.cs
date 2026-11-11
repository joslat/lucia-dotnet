using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lucia.Companion.App;
using Lucia.Companion.App.Controls;
using Lucia.Companion.App.Screens;
using Lucia.Companion.Core;
using Lucia.Companion.Connection;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class SetupContextTests
{
    [AvaloniaTheory]
    [InlineData(390, 620, false)]
    [InlineData(390, 620, true)]
    [InlineData(1194, 800, false)]
    [InlineData(1194, 800, true)]
    [InlineData(1194, 420, true)]
    public void LowerSetupRetainsAVisiblePlaceSelectorAndAnUsableFormViewport(int width, int height, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var editor = new HomeEditorScreen();
        var window = new Window { Width = width, Height = height, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            editor.FindControl<Button>("RemovePlace")!.BringIntoView(); Dispatcher.UIThread.RunJobs();
            var form = editor.FindControl<ScrollViewer>("EditorScroll")!;
            Assert.True(form.Offset.Y > 100, "The journey must reach the actual lower form.");
            Assert.True(form.Viewport.Height >= 96, $"Only {form.Viewport.Height} units remain for editing.");
            AssertInside(SelectedControl(editor), window);
            AssertInside(editor.FindControl<Button>("SaveHome")!, window);
            NativeCapture.Save(window, $"setup-context-{width}-{height}-{large}.png");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(1194, false)]
    [InlineData(1194, true)]
    public void KeyboardSwitchAtLowerFormRetainsValidDraftEditsAndSelectionFocus(int width, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var original = SampleHome.Create();
        var editor = new HomeEditorScreen(original);
        HomeTopology? saved = null; editor.SaveRequested += home => saved = home;
        var window = new Window { Width = width, Height = 800, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            editor.FindControl<TextBox>("PlaceNameInput")!.Text = "Sitting room";
            editor.FindControl<Button>("RemovePlace")!.BringIntoView(); Dispatcher.UIThread.RunJobs();
            AssertInside(SelectedControl(editor), window);
            Assert.True(SelectedControl(editor).Focus()); PressKey(window, Key.Down);
            Assert.Equal(original.Places[1].Name, editor.FindControl<TextBox>("PlaceNameInput")!.Text);
            AssertInside(SelectedControl(editor), window);
            Assert.True(Selector(editor).IsKeyboardFocusWithin);
            PressKey(window, Key.Up);
            Assert.Equal("Sitting room", editor.FindControl<TextBox>("PlaceNameInput")!.Text);
            Assert.True(Selector(editor).IsKeyboardFocusWithin);
            Assert.Null(saved); Assert.Equal("Living room", original.Places[0].Name);
            editor.FindControl<Button>("SaveHome")!.Focus(); PressKey(window, Key.Enter);
            Assert.NotNull(saved); Assert.Equal("Sitting room", saved.Places[0].Name);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(1194, false)]
    [InlineData(1194, true)]
    public void RejectedSelectionKeepsItsPlaceAndRevealsTheErrorBesideTheInvalidInput(int width, bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var editor = new HomeEditorScreen();
        var window = new Window { Width = width, Height = 800, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var input = editor.FindControl<TextBox>("PlaceNameInput")!; input.Text = " ";
            editor.FindControl<Button>("RemovePlace")!.BringIntoView(); Dispatcher.UIThread.RunJobs();
            SelectedControl(editor).Focus(); PressKey(window, Key.Down);
            Assert.Equal(0, editor.FindControl<ListBox>("PlacesList")!.SelectedIndex);
            Assert.Equal(0, editor.FindControl<ComboBox>("PlaceChooser")!.SelectedIndex);
            Assert.Equal(" ", input.Text);
            AssertInside(editor.FindControl<TextBlock>("ErrorText")!, window);
            AssertInside(input, window); Assert.Same(input, window.FocusManager!.GetFocusedElement());
            NativeCapture.Save(window, $"setup-validation-{width}-{large}.png");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResizingTransfersFocusedSelectionToItsVisibleEquivalent(bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var editor = new HomeEditorScreen();
        var window = new Window { Width = 1194, Height = 800, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            Assert.True(editor.FindControl<ListBox>("PlacesList")!.IsVisible);
            SelectedControl(editor).Focus();
            window.Width = 390; Dispatcher.UIThread.RunJobs();
            Assert.Same(editor.FindControl<ComboBox>("PlaceChooser"), window.FocusManager!.GetFocusedElement());
            AssertInside(SelectedControl(editor), window);
            window.Width = 1194; Dispatcher.UIThread.RunJobs();
            Assert.Same(SelectedControl(editor), window.FocusManager.GetFocusedElement());
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(390)]
    [InlineData(1194)]
    public void EmptySetupKeepsHomeMetadataAndCanAddItsFirstPlace(int width)
    {
        CompanionTypography.Apply(size => size * 2);
        var editor = new HomeEditorScreen(new HomeTopology("new-home", "My home", [], [], []));
        var window = new Window { Width = width, Height = 620, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var name = editor.FindControl<TextBox>("HomeNameInput")!;
            name.BringIntoView(); Dispatcher.UIThread.RunJobs(); AssertInside(name, window);
            var add = editor.FindControl<Button>("AddPlace")!;
            add.BringIntoView(); Dispatcher.UIThread.RunJobs(); AssertInside(add, window);
            add.Focus(); PressKey(window, Key.Enter);
            var placeName = editor.FindControl<TextBox>("PlaceNameInput")!;
            Assert.Equal("New place", placeName.Text);
            Assert.Same(placeName, window.FocusManager!.GetFocusedElement()); AssertInside(placeName, window);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(390, false)]
    [InlineData(390, true)]
    [InlineData(1194, false)]
    [InlineData(1194, true)]
    public void LongNamesAndKeyboardLeaveContextAndTheFocusedFieldUsable(int width, bool dark)
    {
        var previousTheme = Application.Current!.RequestedThemeVariant;
        Application.Current.RequestedThemeVariant = dark ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        CompanionTypography.Apply(size => size * 2);
        var sample = SampleHome.Create();
        var longName = new string('M', 200);
        var editor = new HomeEditorScreen(new HomeTopology(sample.Id, sample.Name,
            [sample.Places[0] with { Name = longName }], [], []));
        var viewport = new InputPaneViewport { Child = editor };
        var window = new Window { Width = width, Height = 844, Content = viewport };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var input = editor.FindControl<TextBox>("PlaceNameInput")!;
            input.Focus(); viewport.SetOcclusion(new Rect(0, 544, width, 300)); Dispatcher.UIThread.RunJobs();
            Assert.Equal(longName, input.Text);
            AssertInside(SelectedControl(editor), window); AssertInside(input, window);
            Assert.True(editor.FindControl<ScrollViewer>("EditorScroll")!.Viewport.Height >= 96);
            NativeCapture.Save(window, $"setup-long-name-ime-{width}-{dark}.png");
            viewport.SetOcclusion(default); Dispatcher.UIThread.RunJobs();
            AssertInside(SelectedControl(editor), window); Assert.Same(input, window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); Application.Current.RequestedThemeVariant = previousTheme; }
    }

    [AvaloniaTheory]
    [InlineData("outside")]
    [InlineData("hide")]
    [InlineData("detach")]
    public void RejectedSelectionCannotStealFocusAfterContextChanges(string change)
    {
        var editor = new HomeEditorScreen();
        var outside = new Button { Content = "Elsewhere" };
        var parent = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { editor, outside } };
        Grid.SetRow(outside, 1);
        var window = new Window { Width = 1194, Height = 800, Content = parent };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            editor.FindControl<TextBox>("PlaceNameInput")!.Text = " ";
            SelectedControl(editor).Focus();
            editor.FindControl<ListBox>("PlacesList")!.SelectedIndex = 1;
            if (change == "hide") { editor.IsVisible = false; }
            if (change == "detach") { parent.Children.Remove(editor); }
            Assert.True(outside.Focus()); Dispatcher.UIThread.RunJobs();
            Assert.Same(outside, window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManagementActionRetainsVisibleFocusAcrossReparenting(bool import)
    {
        var catalog = new HomeCatalog("home-a", DateTimeOffset.UtcNow, []);
        var editor = new HomeEditorScreen(SampleHome.Create(), importCatalog: import ? catalog : null);
        var window = new Window { Width = 1194, Height = 800, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var action = editor.FindControl<Button>(import ? "ImportPlaces" : "AddPlace")!;
            Assert.True(action.Focus());
            window.Width = 390; Dispatcher.UIThread.RunJobs();
            Assert.Same(action, window.FocusManager!.GetFocusedElement()); AssertInside(action, window);
            window.Width = 1194; Dispatcher.UIThread.RunJobs();
            Assert.Same(action, window.FocusManager.GetFocusedElement()); AssertInside(action, window);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidSelectionThroughTheOpenChooserReturnsToTheVisibleInvalidInput(bool large)
    {
        CompanionTypography.Apply(size => large ? size * 2 : size);
        var editor = new HomeEditorScreen();
        var window = new Window { Width = 390, Height = 800, Content = editor };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var input = editor.FindControl<TextBox>("PlaceNameInput")!; input.Text = " ";
            editor.FindControl<Button>("RemovePlace")!.BringIntoView(); Dispatcher.UIThread.RunJobs();
            var chooser = editor.FindControl<ComboBox>("PlaceChooser")!;
            chooser.Focus(); PressKey(window, Key.Enter); Assert.True(chooser.IsDropDownOpen);
            PressKey(window, Key.Down); PressKey(window, Key.Enter);
            Assert.False(chooser.IsDropDownOpen);
            Assert.Equal(0, chooser.SelectedIndex);
            Assert.Equal(0, editor.FindControl<ListBox>("PlacesList")!.SelectedIndex);
            Assert.Equal(" ", input.Text);
            Assert.Same(input, window.FocusManager!.GetFocusedElement());
            AssertInside(input, window); AssertInside(editor.FindControl<TextBlock>("ErrorText")!, window);
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DismissingTheKeyboardCannotReplaceAnOpenPlaceChooser(bool invalid)
    {
        CompanionTypography.Apply(size => size * 2);
        var editor = new HomeEditorScreen();
        var viewport = new InputPaneViewport { Child = editor };
        var window = new Window { Width = 1194, Height = 844, Content = viewport };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var input = editor.FindControl<TextBox>("PlaceNameInput")!;
            if (invalid) { input.Text = " "; }
            input.Focus(); viewport.SetOcclusion(new Rect(0, 344, 1194, 500)); Dispatcher.UIThread.RunJobs();
            var chooser = editor.FindControl<ComboBox>("PlaceChooser")!;
            Assert.True(chooser.IsVisible); chooser.Focus(); PressKey(window, Key.Enter);
            Assert.True(chooser.IsDropDownOpen);
            viewport.SetOcclusion(default); Dispatcher.UIThread.RunJobs();
            Assert.True(chooser.IsVisible); Assert.True(chooser.IsDropDownOpen); AssertInside(chooser, window);
            PressKey(window, Key.Down); PressKey(window, Key.Enter);
            Assert.False(chooser.IsDropDownOpen);
            Assert.True(editor.FindControl<ListBox>("PlacesList")!.IsVisible);
            Assert.Equal(invalid ? 0 : 1, editor.FindControl<ListBox>("PlacesList")!.SelectedIndex);
            if (invalid)
            {
                Assert.Same(input, window.FocusManager!.GetFocusedElement());
                AssertInside(input, window); AssertInside(editor.FindControl<TextBlock>("ErrorText")!, window);
            }
            else { Assert.True(Selector(editor).IsKeyboardFocusWithin); }
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData("outside")]
    [InlineData("hide")]
    [InlineData("detach")]
    [InlineData("outside-close")]
    public void PopupReflowCannotStealFocusAfterDismissalOrInterruptedValidation(string change)
    {
        CompanionTypography.Apply(size => size * 2);
        var editor = new HomeEditorScreen();
        var outside = new Button { Content = "Elsewhere" };
        var parent = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { editor, outside } };
        Grid.SetRow(outside, 1);
        var viewport = new InputPaneViewport { Child = parent };
        var window = new Window { Width = 1194, Height = 844, Content = viewport };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var input = editor.FindControl<TextBox>("PlaceNameInput")!;
            if (change != "outside-close") { input.Text = " "; }
            input.Focus(); viewport.SetOcclusion(new Rect(0, 384, 1194, 460)); Dispatcher.UIThread.RunJobs();
            var chooser = editor.FindControl<ComboBox>("PlaceChooser")!;
            chooser.Focus(); PressKey(window, Key.Enter);
            viewport.SetOcclusion(default); Dispatcher.UIThread.RunJobs();
            Assert.True(chooser.IsDropDownOpen);
            if (change != "outside-close") { chooser.SelectedIndex = 1; }
            if (change == "hide") { editor.IsVisible = false; }
            if (change == "detach") { parent.Children.Remove(editor); }
            Assert.True(outside.Focus());
            if (change == "outside-close") { chooser.IsDropDownOpen = false; }
            Dispatcher.UIThread.RunJobs();
            Assert.Same(outside, window.FocusManager!.GetFocusedElement());
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaTheory]
    [InlineData(390)]
    [InlineData(1194)]
    public void RejectedChoiceKeepsItsExplanationAndInputTogetherAboveTheKeyboard(int width)
    {
        CompanionTypography.Apply(size => size * 2);
        var editor = new HomeEditorScreen();
        var viewport = new InputPaneViewport { Child = editor };
        var window = new Window { Width = width, Height = 844, Content = viewport };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var input = editor.FindControl<TextBox>("PlaceNameInput")!; input.Text = " "; input.Focus();
            viewport.SetOcclusion(new Rect(0, 420, width, 424)); Dispatcher.UIThread.RunJobs();
            var chooser = editor.FindControl<ComboBox>("PlaceChooser")!;
            Assert.True(chooser.IsVisible); chooser.Focus(); PressKey(window, Key.Down);
            Assert.Equal(0, chooser.SelectedIndex); Assert.Same(input, window.FocusManager!.GetFocusedElement());
            AssertInside(input, window); AssertInside(editor.FindControl<TextBlock>("ErrorText")!, window);
            NativeCapture.Save(window, $"setup-validation-ime-{width}.png");
        }
        finally { window.Close(); CompanionTypography.Apply(size => size); }
    }

    [AvaloniaFact]
    public void EmptyHomeValidationRemainsVisibleWithoutAPlaceForm()
    {
        var editor = new HomeEditorScreen(new HomeTopology("empty", "My home", [], [], []));
        var window = new Window { Width = 390, Height = 620, Content = editor };
        var saves = 0; editor.SaveRequested += _ => saves++;
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            editor.FindControl<TextBox>("HomeNameInput")!.Text = " ";
            editor.FindControl<Button>("SaveHome")!.Focus(); PressKey(window, Key.Enter);
            Assert.False(editor.FindControl<Border>("PlaceFormCard")!.IsVisible);
            AssertInside(editor.FindControl<TextBlock>("HomeErrorText")!, window);
            Assert.Equal(0, saves);
        }
        finally { window.Close(); }
    }

    private static Control Selector(HomeEditorScreen editor) => editor.FindControl<ComboBox>("PlaceChooser")!.IsVisible
        ? editor.FindControl<ComboBox>("PlaceChooser")! : editor.FindControl<ListBox>("PlacesList")!;

    private static Control SelectedControl(HomeEditorScreen editor) => Selector(editor) is ListBox list
        ? Assert.IsType<ListBoxItem>(list.SelectedItem) : Selector(editor);

    private static void PressKey(Window window, Key key)
    {
        var physical = key == Key.Down ? PhysicalKey.ArrowDown : key == Key.Up ? PhysicalKey.ArrowUp : PhysicalKey.Enter;
        window.KeyPress(key, RawInputModifiers.None, physical, null); window.KeyRelease(key, RawInputModifiers.None, physical, null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertInside(Control target, Window window)
    {
        Assert.True(target.IsEffectivelyVisible);
        var location = target.TranslatePoint(default, window)!.Value;
        Assert.True(location.X >= -.1 && location.Y >= -.1 && location.X + target.Bounds.Width <= window.Bounds.Width + .1
            && location.Y + target.Bounds.Height <= window.Bounds.Height + .1, $"{target.Name ?? target.GetType().Name} at {location}, size {target.Bounds.Size} outside {window.Bounds.Size}.");
        foreach (var scroll in target.GetVisualAncestors().OfType<ScrollViewer>())
        {
            var point = target.TranslatePoint(default, scroll)!.Value;
            Assert.True(point.Y >= -.1 && point.Y + target.Bounds.Height <= scroll.Viewport.Height + .1,
                $"{target.Name ?? target.GetType().Name} outside {scroll.Name} viewport.");
        }
    }
}
