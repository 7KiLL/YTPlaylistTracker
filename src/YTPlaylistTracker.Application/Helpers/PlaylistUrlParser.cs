using System.Text.RegularExpressions;
using System.Web;

namespace YTPlaylistTracker.Application.Helpers;

public static partial class PlaylistUrlParser
{
    // YouTube playlist IDs: PL, UU, LL, FL, OL, RD, UL, TL, OLAK5uy_ prefixes + alphanumeric/dash/underscore
    [GeneratedRegex(@"^(PL|UU|LL|FL|OL|RD|UL|TL|OLAK5uy_)[a-zA-Z0-9_-]{10,}$")]
    private static partial Regex PlaylistIdPattern();

    public static string ExtractPlaylistId(string input)
    {
        input = input.Trim();

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            var query = HttpUtility.ParseQueryString(uri.Query);
            var listParam = query["list"];
            if (!string.IsNullOrWhiteSpace(listParam))
                return listParam;
        }

        return input;
    }

    public static bool IsValidPlaylistId(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && PlaylistIdPattern().IsMatch(id);
    }
}
