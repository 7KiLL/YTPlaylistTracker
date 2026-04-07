using NStack;

namespace YTPlaylistTracker.UI;

internal static class UnicodeWidth
{
    public static int GetWidth(string s)
    {
        int width = 0;
        foreach (var c in s)
            width += Rune.ColumnWidth(c);
        return width;
    }

    public static string Truncate(string s, int maxDisplayWidth)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int width = 0;
        for (int i = 0; i < s.Length; i++)
        {
            int cw = Rune.ColumnWidth(s[i]);
            if (width + cw > maxDisplayWidth - 2)
                return s[..i] + "..";
            width += cw;
        }
        return s;
    }
}
