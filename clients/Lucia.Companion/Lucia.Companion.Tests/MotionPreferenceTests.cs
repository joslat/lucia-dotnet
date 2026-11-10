using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Lucia.Companion.App;
using Lucia.Companion.App.Persistence;
using Lucia.Companion.App.Screens;
using Xunit;

namespace Lucia.Companion.Tests;

public sealed class MotionPreferenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "lucia-motion-" + Guid.NewGuid().ToString("N"));
    private string PreferencePath => Path.Combine(directory, "motion.json");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChoiceSurvivesNewAppSettings(bool reduced)
    {
        var first = new MotionSettings(new LocalMotionPreferenceStore(PreferencePath));
        first.ReduceMotion = !reduced;
        first.ReduceMotion = reduced;

        var restarted = new MotionSettings(new LocalMotionPreferenceStore(PreferencePath));
        Assert.Equal(reduced, restarted.ReduceMotion);
        Assert.Equal(!reduced, restarted.IsEnabled);
        Assert.False(restarted.IsPaused);
        Assert.False(restarted.HasStorageError);
    }

    [Fact]
    public void MissingPreferenceKeepsTheExistingDefaultWithoutWritingOnLoad()
    {
        var motion = new MotionSettings(new LocalMotionPreferenceStore(PreferencePath));
        Assert.False(motion.ReduceMotion);
        Assert.False(motion.HasStorageError);
        Assert.False(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    public void UnreadablePreferenceReducesMotionAndPreservesTheFile(string content)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(PreferencePath, content);
        var motion = new MotionSettings(new LocalMotionPreferenceStore(PreferencePath));

        Assert.True(motion.ReduceMotion);
        Assert.False(motion.IsEnabled);
        Assert.True(motion.HasStorageError);
        Assert.Contains("couldn't be read", motion.StorageStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(content, File.ReadAllText(PreferencePath));

        motion.ReduceMotion = false;
        Assert.False(motion.HasStorageError);
        Assert.False(new MotionSettings(new LocalMotionPreferenceStore(PreferencePath)).ReduceMotion);
    }

    [Fact]
    public void FailedSaveKeepsTheChoiceForThisSessionAndCanRetryIt()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(PreferencePath, "false");
        var motion = new MotionSettings(new LocalMotionPreferenceStore(PreferencePath));
        using (var locked = new FileStream(PreferencePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            motion.ReduceMotion = true;
            Assert.True(motion.ReduceMotion);
            Assert.True(motion.HasStorageError);
            Assert.Contains("this session", motion.StorageStatus);
            Assert.Single(Directory.GetFiles(directory));
        }
        Assert.Equal("false", File.ReadAllText(PreferencePath));

        motion.ReduceMotion = true;
        Assert.False(motion.HasStorageError);
        Assert.True(new MotionSettings(new LocalMotionPreferenceStore(PreferencePath)).ReduceMotion);
    }

    [Fact]
    public void BackgroundPauseDoesNotOverwriteThePreference()
    {
        var motion = new MotionSettings(new LocalMotionPreferenceStore(PreferencePath));
        motion.ReduceMotion = true;
        motion.ReduceMotion = false;
        var before = File.ReadAllBytes(PreferencePath);
        motion.Pause();
        motion.Resume();
        Assert.Equal(before, File.ReadAllBytes(PreferencePath));
        motion.Pause();
        var restarted = new MotionSettings(new LocalMotionPreferenceStore(PreferencePath));
        Assert.False(restarted.ReduceMotion);
        Assert.False(restarted.IsPaused);
        Assert.True(restarted.IsEnabled);
    }

    [AvaloniaTheory]
    [InlineData(390, 844, false)]
    [InlineData(390, 844, true)]
    [InlineData(1194, 834, false)]
    [InlineData(1194, 834, true)]
    public void MoreRestoresTheChoiceAndExplainsFailedStorage(int width, int height, bool dark)
    {
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var motion = new MotionSettings(new LocalMotionPreferenceStore(PreferencePath));
        motion.ReduceMotion = true;
        var restored = new MotionSettings(new LocalMotionPreferenceStore(PreferencePath));
        var more = new MoreScreen(isDemo: true, restored);
        var window = new Window { Width = width, Height = height, Content = more };
        try
        {
            window.Show();
            var toggle = more.FindControl<CheckBox>("ReduceMotion")!;
            var status = more.FindControl<TextBlock>("MotionStorageStatus")!;
            Assert.True(toggle.IsChecked);
            Assert.Contains("saved on this device", status.Text);
            using (var locked = new FileStream(PreferencePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                toggle.IsChecked = false;
                Assert.False(restored.ReduceMotion);
                Assert.Contains("this session", status.Text);
                NativeCapture.Save(window, $"motion-preference-failed-{width}-{(dark ? "dark" : "light")}.png");
            }
            toggle.IsChecked = true;
            Assert.Contains("saved on this device", status.Text);
            NativeCapture.Save(window, $"motion-preference-saved-{width}-{(dark ? "dark" : "light")}.png");
        }
        finally { window.Close(); }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) { Directory.Delete(directory, recursive: true); }
    }
}
