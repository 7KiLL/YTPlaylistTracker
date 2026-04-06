using YTPlaylistTracker.Application.Helpers;

namespace YTPlaylistTracker.UnitTests.Helpers;

public class PlaylistUrlParserTests
{
    [Fact]
    public void ExtractPlaylistId_FullUrl_ExtractsId()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId(
            "https://www.youtube.com/playlist?list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf");
        Assert.Equal("PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf", result);
    }

    [Fact]
    public void ExtractPlaylistId_BareId_ReturnedAsIs()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId("PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf");
        Assert.Equal("PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf", result);
    }

    [Fact]
    public void ExtractPlaylistId_UrlWithExtraParams_CorrectId()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId(
            "https://www.youtube.com/playlist?list=PLtest123&si=abc&index=5");
        Assert.Equal("PLtest123", result);
    }

    [Fact]
    public void ExtractPlaylistId_WhitespaceInput_Trimmed()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId("  PLtest123  ");
        Assert.Equal("PLtest123", result);
    }

    [Fact]
    public void ExtractPlaylistId_VideoUrlWithList_ExtractsId()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLtest123");
        Assert.Equal("PLtest123", result);
    }

    [Fact]
    public void ExtractPlaylistId_InvalidUrl_ReturnsInput()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId("not-a-url-or-id");
        Assert.Equal("not-a-url-or-id", result);
    }
}
