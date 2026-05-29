namespace YTPlaylistTracker.E2ETests.Harness;

internal static class ScreenCapture
{
    /// <summary>
    /// Forces a layout+draw cycle and captures the rendered screen buffer as plain text.
    /// Trailing whitespace per line is trimmed to avoid diff noise.
    /// </summary>
    public static string Capture(IApplication app)
    {
        app.LayoutAndDraw(true); // forceRedraw: deterministic full draw for snapshot capture

        var raw = app.Driver!.ToString();

        var lines = raw.Split('\n')
            .Select(l => l.TrimEnd())
            .ToArray();

        var lastNonEmpty = Array.FindLastIndex(lines, l => l.Length > 0);

        return lastNonEmpty < 0
            ? string.Empty
            : string.Join('\n', lines.AsSpan(0, lastNonEmpty + 1));
    }
}
