namespace YTPlaylistTracker.UI;

internal enum GlyphMode
{
    Full,
    Basic,
}

internal static class GlyphDetector
{
    private static GlyphMode? _cached;
    private static string _userOverride = "";

    internal static void SetUserOverride(string mode)
    {
        _userOverride = mode;
        _cached = null;
    }

    internal static GlyphMode Detect(Func<string, string?>? envReader = null)
    {
        if (envReader is null && _cached.HasValue)
            return _cached.Value;

        var mode = ResolveOverride(_userOverride) ?? DetectCore(envReader ?? Environment.GetEnvironmentVariable);

        if (envReader is null)
            _cached = mode;

        return mode;
    }

    internal static void Reset() => _cached = null;

    private static GlyphMode? ResolveOverride(string setting) => setting switch
    {
        "full" => GlyphMode.Full,
        "basic" => GlyphMode.Basic,
        _ => null,
    };

    private static GlyphMode DetectCore(Func<string, string?> env)
    {
        if (!OperatingSystem.IsWindows())
            return GlyphMode.Full;

        if (!string.IsNullOrEmpty(env("WT_SESSION")))
            return GlyphMode.Full;

        if (!string.IsNullOrEmpty(env("ConEmuPID")))
            return GlyphMode.Full;

        var termProgram = env("TERM_PROGRAM") ?? "";
        if (termProgram is "mintty" or "WezTerm" or "Alacritty" or "ghostty")
            return GlyphMode.Full;

        return GlyphMode.Basic;
    }
}
