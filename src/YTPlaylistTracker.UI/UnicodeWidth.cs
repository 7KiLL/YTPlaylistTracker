using System.Buffers;
using Terminal.Gui;
using TextRune = System.Text.Rune;

namespace YTPlaylistTracker.UI;

/// <summary>
/// Measures and truncates strings for Terminal.Gui display,
/// respecting Unicode character widths (CJK, emoji, etc.).
/// </summary>
internal static class UnicodeWidth
{
    public static int GetWidth(string s)
    {
        int width = 0;
        for (int i = 0; i < s.Length;)
        {
            var status = TextRune.DecodeFromUtf16(s.AsSpan(i), out var rune, out var charsConsumed);
            if (status != OperationStatus.Done)
                break;
            width += rune.GetColumns();
            i += charsConsumed;
        }
        return width;
    }

    public static string Truncate(string s, int maxDisplayWidth)
    {
        if (s is null or "") return s!;
        if (GetWidth(s) <= maxDisplayWidth) return s;

        int width = 0;
        for (int i = 0; i < s.Length;)
        {
            var status = TextRune.DecodeFromUtf16(s.AsSpan(i), out var rune, out var charsConsumed);
            if (status != OperationStatus.Done)
                break;
            int cw = rune.GetColumns();
            if (width + cw > maxDisplayWidth - 2)
                return s[..i] + "..";
            width += cw;
            i += charsConsumed;
        }
        return s;
    }
}
