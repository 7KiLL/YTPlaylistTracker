using YTPlaylistTracker.Application.Helpers;

namespace YTPlaylistTracker.UnitTests.Helpers;

public class PlaylistUrlParserTests
{
    [Test]
    public async Task ExtractPlaylistId_FullUrl_ExtractsId()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId(
            "https://www.youtube.com/playlist?list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf");
        await Assert.That(result).IsEqualTo("PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf");
    }

    [Test]
    public async Task ExtractPlaylistId_BareId_ReturnedAsIs()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId("PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf");
        await Assert.That(result).IsEqualTo("PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf");
    }

    [Test]
    public async Task ExtractPlaylistId_UrlWithExtraParams_CorrectId()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId(
            "https://www.youtube.com/playlist?list=PLtest123&si=abc&index=5");
        await Assert.That(result).IsEqualTo("PLtest123");
    }

    [Test]
    public async Task ExtractPlaylistId_WhitespaceInput_Trimmed()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId("  PLtest123  ");
        await Assert.That(result).IsEqualTo("PLtest123");
    }

    [Test]
    public async Task ExtractPlaylistId_VideoUrlWithList_ExtractsId()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId(
            "https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLtest123");
        await Assert.That(result).IsEqualTo("PLtest123");
    }

    [Test]
    public async Task ExtractPlaylistId_InvalidUrl_ReturnsInput()
    {
        var result = PlaylistUrlParser.ExtractPlaylistId("not-a-url-or-id");
        await Assert.That(result).IsEqualTo("not-a-url-or-id");
    }
}
