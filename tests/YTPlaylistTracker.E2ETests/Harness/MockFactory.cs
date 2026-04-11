using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Platform;
using YTPlaylistTracker.Infrastructure.YouTube;
using YTPlaylistTracker.UI.Views;

namespace YTPlaylistTracker.E2ETests.Harness;

internal sealed class MockFactory
{
    public IPlaylistRepository PlaylistRepo { get; } = Substitute.For<IPlaylistRepository>();
    public IProfileRepository ProfileRepo { get; } = Substitute.For<IProfileRepository>();
    public IYouTubeApiServiceFactory YouTubeFactory { get; } = Substitute.For<IYouTubeApiServiceFactory>();
    public ISystemLauncher SystemLauncher { get; } = Substitute.For<ISystemLauncher>();
    public IUserSettings UserSettings { get; } = Substitute.For<IUserSettings>();
    public IUpdateService UpdateService { get; } = Substitute.For<IUpdateService>();
    public IServiceScopeFactory ScopeFactory { get; private set; } = null!;
    public ILogger<MainWindow> Logger { get; } = Substitute.For<ILogger<MainWindow>>();

    public MockFactory()
    {
        ProfileRepo.GetAllAsync().Returns(new List<Profile>());
        PlaylistRepo.GetByProfileAsync(Arg.Any<int>()).Returns(new List<Playlist>());
        PlaylistRepo.GetVideosAsync(Arg.Any<int>()).Returns(new List<Video>());
        PlaylistRepo.GetDeletedVideosAsync(Arg.Any<int>()).Returns(new List<Video>());

        YouTubeFactory.IsAuthenticated(Arg.Any<Profile>()).Returns(false);

        UserSettings.ThemeName.Returns("Catppuccin");
        UserSettings.AutoSyncOnStartup.Returns(false);
        UserSettings.SortTrackedFirst.Returns(false);
        UserSettings.GlyphMode.Returns("basic");

        UpdateService.CheckForUpdateAsync()
            .Returns(new UpdateInfo("0.0.0", "0.0.0", "", false));

        SetupScopeFactory();
    }

    private void SetupScopeFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton(PlaylistRepo);
        services.AddSingleton(ProfileRepo);
        services.AddSingleton(Substitute.For<ISyncService>());
        var sp = services.BuildServiceProvider();

        ScopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(sp);
        ScopeFactory.CreateScope().Returns(scope);
    }
}
