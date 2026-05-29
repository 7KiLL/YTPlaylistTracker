using YTPlaylistTracker.E2ETests.Screens;
using YTPlaylistTracker.UI;
using YTPlaylistTracker.UI.Views;

namespace YTPlaylistTracker.E2ETests.Harness;

internal sealed class AppHarness : IAsyncDisposable
{
    public MainWindow Window { get; }
    public MockFactory Mocks { get; }
    public MainScreen Screen { get; }

    private readonly IApplication _app;
    private readonly SessionToken _session;

    private AppHarness(MainWindow window, MockFactory mocks, IApplication app, SessionToken session)
    {
        Window = window;
        Mocks = mocks;
        _app = app;
        _session = session;
        Screen = new MainScreen(window);
    }

    internal static async Task<AppHarness> CreateAsync(MockFactory mocks, int cols = 120, int rows = 30)
    {
        // 2.4.3: instance-based app. The static Application API is deprecated.
        var app = TGuiApp.Create();
        app.Init(DriverRegistry.Names.ANSI);

        // Set deterministic terminal size for snapshot testing.
        // rc.1 replaced OutputBuffer.SetSize with Driver.SetScreenSize.
        app.Driver!.SetScreenSize(cols, rows);

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

        await window.InitializeAsync(app);

        // Begin a session to register the window as the active top-level and set up
        // focus/layout, WITHOUT entering the run loop. The session stays open for the
        // harness lifetime so the window remains the active runnable that
        // ScreenCapture can draw. (2.0.1 used StopAfterFirstIteration+Run; on the
        // instance app that ends the session, leaving the draw buffer empty.)
        var session = app.Begin(window);

        return new AppHarness(window, mocks, app, session);
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
    public string CaptureScreen() => ScreenCapture.Capture(_app);

    public ValueTask DisposeAsync()
    {
        _app.End(_session);
        Window.Dispose();
        _app.Dispose();
        return ValueTask.CompletedTask;
    }
}
