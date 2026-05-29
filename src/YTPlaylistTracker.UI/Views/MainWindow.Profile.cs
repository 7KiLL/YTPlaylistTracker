using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Configuration;
using YTPlaylistTracker.Infrastructure.YouTube;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private void HandleWelcomeChoice(WelcomeChoice choice)
    {
        if (_selectedProfile is null) return;

        switch (choice)
        {
            case WelcomeChoice.SignIn:
                _selectedProfile.IsOffline = false;
                profileRepo.UpdateAsync(_selectedProfile).GetAwaiter().GetResult();
                OnLoginProfile();
                break;

            case WelcomeChoice.Offline:
                _selectedProfile.Name = "Offline";
                _selectedProfile.IsOffline = true;
                profileRepo.UpdateAsync(_selectedProfile).GetAwaiter().GetResult();
                _profiles = profileRepo.GetAllAsync().GetAwaiter().GetResult().ToList();
                _selectedProfile = _profiles.FirstOrDefault(p => p.IsDefault) ?? _profiles[0];
                RefreshProfileList();
                break;
        }
    }

    private void ShowProfileContextMenu()
    {
        if (_selectedProfile is null) return;

        var isAuth = youtubeApiFactory.IsAuthenticated(_selectedProfile);
        var loginLabel = isAuth ? "Logout" : "Login with Google";

        var popover = new PopoverMenu(new MenuItem[]
        {
            new("View _Details", "", () => ShowDetail()),
            new(loginLabel + " (_L)", "", () => OnToggleLogin()),
            new("_Rename (r)", "", () => OnRenameProfile()),
            new("_New profile (n)", "", () => OnNewProfile()),
            new("Set _default (d)", "", () => OnSetDefaultProfile()),
            new("Delete (x)", "", () => OnDeleteProfile()),
        });
        _app.Popovers?.Register(popover);
        popover.MakeVisible();
    }

    private void OnNewProfile()
    {
        var inputValue = Dialogs.PromptForText(this, "New Profile", "Create",
            fieldLabel: "Name:", height: 9);
        if (string.IsNullOrWhiteSpace(inputValue)) return;

        _ = Task.Run(async () =>
        {
            await using var bgScope = scopeFactory.CreateAsyncScope();
            var bgProfileRepo = bgScope.ServiceProvider.GetRequiredService<IProfileRepository>();
            var profile = new Profile { Name = inputValue!, IsDefault = false, IsOffline = true };
            await bgProfileRepo.AddAsync(profile).ConfigureAwait(false);
            _profiles = (await bgProfileRepo.GetAllAsync().ConfigureAwait(false)).ToList();

            _app.Invoke(() =>
            {
                RefreshProfileList();
                // Select the new profile
                var idx = _profiles.FindIndex(p => p.Name == inputValue);
                if (idx >= 0)
                {
                    _profileList.SelectedItem = idx;
                    _selectedProfile = _profiles[idx];
                }
            });
        });
    }

    private void OnToggleLogin()
    {
        if (_selectedProfile is null) return;

        if (youtubeApiFactory.IsAuthenticated(_selectedProfile))
            OnLogoutProfile();
        else
            OnLoginProfile();
    }

    private void OnLoginProfile()
    {
        if (_selectedProfile is null) return;

        if (string.IsNullOrWhiteSpace(AppSettings.OAuthClientId) || string.IsNullOrWhiteSpace(AppSettings.OAuthClientSecret))
        { Dialogs.Query(_app, "OAuth Not Configured", "OAuth credentials not configured.\nSet YTPT_CLIENT_ID and YTPT_CLIENT_SECRET env vars.", "OK"); return; }

        var profile = _selectedProfile;
        Dialog? urlDialog = null;

        _ = Task.Run(async () =>
        {
            try
            {
                var service = await youtubeApiFactory.LoginAsync(profile, authUrl =>
                {
                    _app.Invoke(() =>
                    {
                        urlDialog = new Dialog() { Title = "", Width = Dim.Percent(80), Height = 11 };
                        urlDialog.Border!.Settings &= ~BorderSettings.Title;
                        urlDialog.Add(new Label { Text = " Login with Google", X = 0, Y = 0, Width = Dim.Fill(), SchemeName = Theme.SchemeFrame });
                        urlDialog.Add(new Label() { Text = "A browser window should open. If not, copy this URL:", X = 1, Y = 1 });
                        var urlField = new TextField() { Text = authUrl, X = 1, Y = 3, Width = Dim.Fill(2), ReadOnly = true };
                        urlDialog.Add(urlField);
                        var copyBtn = new Button() { Text = "Copy URL", X = 1, Y = 5 };
                        copyBtn.Accepting += (sender, e) =>
                        {
                            if (browser.TryCopyToClipboard(authUrl, out var err)) copyBtn.Text = "Copied!";
                            else Dialogs.Query(_app, "Copy Failed", err!);
                        };
                        var openBtn = new Button() { Text = "Open in Browser", X = 16, Y = 5 };
                        openBtn.Accepting += (sender, e) => browser.OpenUrl(authUrl);
                        urlDialog.Add(copyBtn, openBtn);
                        urlDialog.Add(new Label() { Text = "Waiting for sign-in...", X = 38, Y = 5, SchemeName = "Menu" });
                        _app.Run(urlDialog);
                    });
                }).ConfigureAwait(false);

                var channel = await service.GetMyChannelAsync().ConfigureAwait(false);

                InvokeUI(() =>
                {
                    if (urlDialog is not null) _app.RequestStop();
                    _youtubeApi = service;
                    profile.IsOffline = false;
                    if (channel is not null)
                    {
                        profile.YouTubeChannelId = channel.ChannelId;
                        profile.ChannelTitle = channel.Title;
                        profile.ChannelThumbnailUrl = channel.ThumbnailUrl;
                    }
                    profileRepo.UpdateAsync(profile).GetAwaiter().GetResult();
                    _profiles = profileRepo.GetAllAsync().GetAwaiter().GetResult().ToList();
                    RefreshProfileList();
                    RefreshPlaylistsAsync().GetAwaiter().GetResult();
                    Title = DefaultTitle;
                    SetNeedsDraw();
                    StartBackgroundWork();
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login failed for profile {Profile}", profile.Name);
                InvokeUI(() =>
                {
                    if (urlDialog is not null) _app.RequestStop();
                    Title = DefaultTitle;
                    Dialogs.Query(_app, "Login Failed", ex.Message, "OK");
                });
            }
        });
    }

    private void OnLogoutProfile()
    {
        if (_selectedProfile is null) return;

        var confirm = Dialogs.Query(_app, "Logout",
            $"Remove OAuth tokens for \"{_selectedProfile.ChannelTitle ?? _selectedProfile.Name}\"?\n\nThe profile will switch to offline mode.",
            "Logout", "Cancel");
        if (confirm != 0) return;

        var slug = YouTubeApiServiceFactory.ToProfileSlug(_selectedProfile.Name);
        var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, slug);
        if (Directory.Exists(tokenDir)) Directory.Delete(tokenDir, recursive: true);
        if (_youtubeApi is IDisposable d) d.Dispose();
        _ = Task.Run(async () =>
        {
            _youtubeApi = await youtubeApiFactory.CreateForProfileAsync(_selectedProfile).ConfigureAwait(false);
            _app.Invoke(() => RefreshProfileList());
        });
    }

    private void OnRenameProfile()
    {
        if (_selectedProfile is null) return;

        var inputValue = Dialogs.PromptForText(this, "Rename Profile", "Save",
            fieldLabel: "Name:", defaultValue: _selectedProfile.Name);
        if (string.IsNullOrWhiteSpace(inputValue) || inputValue == _selectedProfile.Name) return;
        _selectedProfile.Name = inputValue!;
        _ = Task.Run(async () =>
        {
            await using var bgScope = scopeFactory.CreateAsyncScope();
            var bgProfileRepo = bgScope.ServiceProvider.GetRequiredService<IProfileRepository>();
            await bgProfileRepo.UpdateAsync(_selectedProfile).ConfigureAwait(false);
            _profiles = (await bgProfileRepo.GetAllAsync().ConfigureAwait(false)).ToList();
            _app.Invoke(() => RefreshProfileList());
        });
    }

    private void OnSetDefaultProfile()
    {
        if (_selectedProfile is null || _selectedProfile.IsDefault) return;
        _ = Task.Run(async () =>
        {
            await using var bgScope = scopeFactory.CreateAsyncScope();
            var bgProfileRepo = bgScope.ServiceProvider.GetRequiredService<IProfileRepository>();
            await bgProfileRepo.SetDefaultAsync(_selectedProfile.Id).ConfigureAwait(false);
            _profiles = (await bgProfileRepo.GetAllAsync().ConfigureAwait(false)).ToList();
            _selectedProfile = _profiles.FirstOrDefault(p => p.Id == _selectedProfile.Id) ?? _selectedProfile;
            _app.Invoke(() => RefreshProfileList());
        });
    }

    private void OnDeleteProfile()
    {
        if (_selectedProfile is null) return;
        if (_profiles.Count <= 1)
        { Dialogs.Query(_app, "Cannot Delete", "You must have at least one profile.", "OK"); return; }

        var confirm = Dialogs.Query(_app, "Delete Profile",
            $"Delete \"{_selectedProfile.ChannelTitle ?? _selectedProfile.Name}\" and all its playlists?\n\nThis cannot be undone.",
            "Delete", "Cancel");
        if (confirm != 0) return;

        var deletedId = _selectedProfile.Id;
        _ = Task.Run(async () =>
        {
            await using var bgScope = scopeFactory.CreateAsyncScope();
            var bgProfileRepo = bgScope.ServiceProvider.GetRequiredService<IProfileRepository>();
            var slug = YouTubeApiServiceFactory.ToProfileSlug(_selectedProfile.Name);
            var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, slug);
            if (Directory.Exists(tokenDir)) Directory.Delete(tokenDir, recursive: true);
            await bgProfileRepo.DeleteAsync(deletedId).ConfigureAwait(false);
            _profiles = (await bgProfileRepo.GetAllAsync().ConfigureAwait(false)).ToList();

            var newDefault = _profiles.FirstOrDefault(p => p.IsDefault) ?? _profiles[0];
            _selectedProfile = newDefault;
            _youtubeApi = await youtubeApiFactory.CreateForProfileAsync(newDefault).ConfigureAwait(false);

            _app.Invoke(() =>
            {
                RefreshProfileList();
                RefreshPlaylistsAsync().GetAwaiter().GetResult();
            });
        });
    }

    private async Task SwitchProfileApiService()
    {
        if (_selectedProfile is null) return;
        if (_youtubeApi is IDisposable d) d.Dispose();
        try
        {
            _youtubeApi = await youtubeApiFactory.CreateForProfileAsync(_selectedProfile).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create API service for profile {Profile}", _selectedProfile.Name);
            _youtubeApi = null;
        }
    }
}
