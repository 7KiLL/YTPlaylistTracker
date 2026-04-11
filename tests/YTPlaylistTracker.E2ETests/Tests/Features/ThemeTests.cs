using YTPlaylistTracker.E2ETests.Harness;
using YTPlaylistTracker.UI;

namespace YTPlaylistTracker.E2ETests.Tests.Features;

[NotInParallel("TerminalGui")]
[Category("Themes")]
public class ThemeTests
{
    private AppHarness? _harness;

    [After(Test)]
    public async Task Teardown()
    {
        if (_harness is not null)
            await _harness.DisposeAsync();
    }

    [Test]
    [Arguments("Catppuccin")]
    [Arguments("Dracula")]
    [Arguments("Gruvbox Dark")]
    [Arguments("Nord")]
    [Arguments("High Contrast Dark")]
    [Arguments("High Contrast Light")]
    public async Task All_Themes_Load_Without_Crash(string themeName)
    {
        _harness = await new AppHarnessBuilder()
            .WithTheme(themeName)
            .BuildAsync();

        await Assert.That(Theme.CurrentName).IsEqualTo(themeName);
    }

    [Test]
    public async Task Unknown_Theme_Falls_Back_To_Default()
    {
        _harness = await new AppHarnessBuilder()
            .WithTheme("Nonexistent")
            .BuildAsync();

        await Assert.That(Theme.CurrentName).IsEqualTo("Catppuccin");
    }
}
