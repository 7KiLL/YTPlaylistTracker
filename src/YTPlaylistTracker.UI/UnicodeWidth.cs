using System.Buffers;
using System.Text;
using Terminal.Gui;
using TextRune = System.Text.Rune;

namespace YTPlaylistTracker.UI;

/// <summary>
/// Measures and truncates strings for Terminal.Gui display,
/// respecting Unicode character widths (CJK, emoji, etc.).
/// Strips variation selectors (U+FE0E/U+FE0F) which cause
/// rendering artifacts in terminal emulators.
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
            if (!IsVariationSelector(rune))
                width += rune.GetColumns();
            i += charsConsumed;
        }
        return width;
    }

    public static string Truncate(string s, int maxDisplayWidth)
    {
        if (s is null or "") return s!;
        s = StripVariationSelectors(s);
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

    /// <summary>
    /// Strips Unicode variation selectors (VS15 text, VS16 emoji) that
    /// terminal emulators render as replacement characters (�).
    /// </summary>
    private static string StripVariationSelectors(string s)
    {
        if (s.IndexOf('\uFE0E') < 0 && s.IndexOf('\uFE0F') < 0)
            return s;

        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] is not ('\uFE0E' or '\uFE0F'))
                sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private static bool IsVariationSelector(TextRune rune) =>
        rune.Value is 0xFE0E or 0xFE0F;
}
