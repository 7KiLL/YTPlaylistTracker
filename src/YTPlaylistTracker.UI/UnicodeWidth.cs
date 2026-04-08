using System.Buffers;
using System.Globalization;
using System.Text;
using Terminal.Gui;
using TextRune = System.Text.Rune;

namespace YTPlaylistTracker.UI;

/// <summary>
/// Measures and truncates strings for Terminal.Gui display.
/// Replaces emoji with text placeholders to avoid NStack width miscalculation.
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
            width += new Rune(rune.Value).GetColumns();
            i += charsConsumed;
        }
        return width;
    }

    public static string Truncate(string s, int maxDisplayWidth)
    {
        if (s is null or "") return s!;
        s = SanitizeForDisplay(s);
        if (GetWidth(s) <= maxDisplayWidth) return s;

        int width = 0;
        for (int i = 0; i < s.Length;)
        {
            var status = TextRune.DecodeFromUtf16(s.AsSpan(i), out var rune, out var charsConsumed);
            if (status != OperationStatus.Done)
                break;
            int cw = new Rune(rune.Value).GetColumns();
            if (width + cw > maxDisplayWidth - 2)
                return s[..i] + "..";
            width += cw;
            i += charsConsumed;
        }
        return s;
    }

    /// <summary>
    /// Replaces astral plane characters (emoji, symbols) with a space.
    /// Terminal.Gui v1 (NStack) has inaccurate width tables for emoji,
    /// causing column misalignment that can't be fixed at our level.
    /// </summary>
    public static string SanitizeForDisplay(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length;)
        {
            var status = TextRune.DecodeFromUtf16(s.AsSpan(i), out var rune, out var charsConsumed);
            if (status != OperationStatus.Done)
                break;

            if (rune.Value > 0xFFFF || TextRune.GetUnicodeCategory(rune) is UnicodeCategory.OtherSymbol)
                sb.Append(' ');
            else
                sb.Append(s.AsSpan(i, charsConsumed));

            i += charsConsumed;
        }
        return sb.ToString();
    }
}
