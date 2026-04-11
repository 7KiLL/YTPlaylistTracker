using YTPlaylistTracker.E2ETests.Screens;
using YTPlaylistTracker.UI;
using YTPlaylistTracker.UI.Views;

namespace YTPlaylistTracker.E2ETests.Harness;

internal sealed class AppHarness : IAsyncDisposable
{
    public MainWindow Window { get; }
    public MockFactory Mocks { get; }
    public MainScreen Screen { get; }

    private AppHarness(MainWindow window, MockFactory mocks)
    {
        Window = window;
        Mocks = mocks;
        Screen = new MainScreen(window);
    }

    internal static async Task<AppHarness> CreateAsync(MockFactory mocks, int cols = 120, int rows = 30)
    {
        TGuiApp.Init(driverName: DriverRegistry.Names.ANSI);

        // Set deterministic terminal size for snapshot testing
        TGuiApp.Driver!.GetOutputBuffer().SetSize(cols, rows);

        Theme.Apply(mocks.UserSettings.ThemeName);

        var window = new MainWindow(
            mocks.PlaylistRepo,
            mocks.ProfileRepo,
            mocks.YouTubeFactory,
            mocks.SystemLauncher,
            mocks.UserSettings,
            mocks.UpdateService,
            mocks.ScopeFactory,
            mocks.Logger);

        await window.InitializeAsync();

        // Run one iteration to register window as top-level and set up focus
        TGuiApp.StopAfterFirstIteration = true;
        TGuiApp.Run(window);

        return new AppHarness(window, mocks);
    }

    /// <summary>
    /// Dispatch key through MainWindow's command handler directly.
    /// Application.RaiseKeyDownEvent doesn't work post-StopAfterFirstIteration,
    /// so we call DispatchKey (the same method Application.KeyDown routes to).
    /// </summary>
    public void SendKey(Key key) => Window.DispatchKey(key);

    public void SendKeys(params Key[] keys)
    {
        foreach (var key in keys) Window.DispatchKey(key);
    }

    public void SendText(string text)
    {
        foreach (var c in text)
            Window.NewKeyDownEvent((Key)c);
    }

    /// <summary>
    /// Captures the current rendered screen as a plain text string.
    /// Forces a layout+draw cycle before reading the buffer.
    /// </summary>
    public string CaptureScreen() => ScreenCapture.Capture();

    public async ValueTask DisposeAsync()
    {
        Window.Dispose();
        TGuiApp.Shutdown();
    }
}
