using Microsoft.Extensions.Logging;
using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;
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

        var menu = new ContextMenu();
        menu.Show(new MenuBarItem("", new MenuItem[]
            {
                new("View _Details", "", () => ShowDetail()),
                new(loginLabel + " (_L)", "", () => OnToggleLogin()),
                new("_Rename (r)", "", () => OnRenameProfile()),
                new("_New profile (n)", "", () => OnNewProfile()),
                new("Set _default (d)", "", () => OnSetDefaultProfile()),
                new("Delete (x)", "", () => OnDeleteProfile()),
            })
        );
    }

    private void OnNewProfile()
    {
        var dialog = new Dialog() { Title = "New Profile", Width = 50, Height = 8 };
        var label = new Label() { Text = "Name:", X = 1, Y = 0 };
        var input = new TextField() { Text = "", X = 8, Y = 0, Width = 36 };
        dialog.Add(label, input);

        var okBtn = new Button() { Text = "Create", IsDefault = true };
        string? inputValue = null;
        okBtn.Accepting += (sender, e) =>
        {
            inputValue = input.Text?.Trim();
            global::Terminal.Gui.Application.RequestStop();
        };
        var cancelBtn = new Button() { Text = "Cancel" };
        cancelBtn.Accepting += (sender, e) => global::Terminal.Gui.Application.RequestStop();
        dialog.AddButton(cancelBtn);
        dialog.AddButton(okBtn);

        global::Terminal.Gui.Application.Run(dialog);

        if (string.IsNullOrWhiteSpace(inputValue)) return;

        _ = Task.Run(async () =>
        {
            var profile = new Profile { Name = inputValue!, IsDefault = false, IsOffline = true };
            await profileRepo.AddAsync(profile).ConfigureAwait(false);
            _profiles = (await profileRepo.GetAllAsync().ConfigureAwait(false)).ToList();

            global::Terminal.Gui.Application.Invoke(() =>
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
        { MessageBox.Query("OAuth Not Configured", "OAuth credentials not configured.\nSet YTPT_CLIENT_ID and YTPT_CLIENT_SECRET env vars.", "OK"); return; }

        var profile = _selectedProfile;
        Dialog? urlDialog = null;

        _ = Task.Run(async () =>
        {
            try
            {
                var service = await youtubeApiFactory.LoginAsync(profile, authUrl =>
                {
                    global::Terminal.Gui.Application.Invoke(() =>
                    {
                        urlDialog = new Dialog() { Title = "Login with Google", Width = 78, Height = 10 };
                        urlDialog.Add(new Label() { Text = "If the browser didn't open:", X = 1, Y = 0 });
                        var urlField = new TextField() { Text = authUrl, X = 1, Y = 2, Width = 72, ReadOnly = true };
                        urlField.SelectAll();
                        urlDialog.Add(urlField);
                        var copyBtn = new Button() { Text = "Copy URL", X = 1, Y = 4 };
                        copyBtn.Accepting += (sender, e) =>
                        {
                            if (browser.TryCopyToClipboard(authUrl, out var err)) copyBtn.Text = "Copied!";
                            else MessageBox.Query("Copy Failed", err!, "OK");
                        };
                        var openBtn = new Button() { Text = "Open in Browser", X = 16, Y = 4 };
                        openBtn.Accepting += (sender, e) => browser.OpenUrl(authUrl);
                        urlDialog.Add(copyBtn, openBtn);
                        urlDialog.Add(new Label() { Text = "Waiting for sign-in...", X = 38, Y = 4, ColorScheme = Colors.ColorSchemes["Menu"] });
                        global::Terminal.Gui.Application.Run(urlDialog);
                    });
                }).ConfigureAwait(false);

                var channel = await service.GetMyChannelAsync().ConfigureAwait(false);

                InvokeUI(() =>
                {
                    if (urlDialog is not null) global::Terminal.Gui.Application.RequestStop();
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
                    global::Terminal.Gui.Application.Top?.SetNeedsDraw();
                    StartBackgroundWork();
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Login failed for profile {Profile}", profile.Name);
                InvokeUI(() =>
                {
                    if (urlDialog is not null) global::Terminal.Gui.Application.RequestStop();
                    Title = DefaultTitle;
                    MessageBox.Query("Login Failed", ex.Message, "OK");
                });
            }
        });
    }

    private void OnLogoutProfile()
    {
        if (_selectedProfile is null) return;

        var confirm = MessageBox.Query("Logout",
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
            global::Terminal.Gui.Application.Invoke(() => RefreshProfileList());
        });
    }

    private void OnRenameProfile()
    {
        if (_selectedProfile is null) return;

        var dialog = new Dialog() { Title = "Rename Profile", Width = 50, Height = 7 };
        var label = new Label() { Text = "Name:", X = 1, Y = 0 };
        var input = new TextField() { Text = _selectedProfile.Name, X = 8, Y = 0, Width = 36 };
        dialog.Add(label, input);

        var okBtn = new Button() { Text = "Save", IsDefault = true };
        string? inputValue = null;
        okBtn.Accepting += (sender, e) =>
        {
            inputValue = input.Text?.Trim();
            global::Terminal.Gui.Application.RequestStop();
        };
        var cancelBtn = new Button() { Text = "Cancel" };
        cancelBtn.Accepting += (sender, e) => global::Terminal.Gui.Application.RequestStop();
        dialog.AddButton(cancelBtn);
        dialog.AddButton(okBtn);

        global::Terminal.Gui.Application.Run(dialog);

        if (string.IsNullOrWhiteSpace(inputValue) || inputValue == _selectedProfile.Name) return;
        _selectedProfile.Name = inputValue!;
        _ = Task.Run(async () =>
        {
            await profileRepo.UpdateAsync(_selectedProfile).ConfigureAwait(false);
            _profiles = (await profileRepo.GetAllAsync().ConfigureAwait(false)).ToList();
            global::Terminal.Gui.Application.Invoke(() => RefreshProfileList());
        });
    }

    private void OnSetDefaultProfile()
    {
        if (_selectedProfile is null || _selectedProfile.IsDefault) return;
        _ = Task.Run(async () =>
        {
            await profileRepo.SetDefaultAsync(_selectedProfile.Id).ConfigureAwait(false);
            _profiles = (await profileRepo.GetAllAsync().ConfigureAwait(false)).ToList();
            _selectedProfile = _profiles.FirstOrDefault(p => p.Id == _selectedProfile.Id) ?? _selectedProfile;
            global::Terminal.Gui.Application.Invoke(() => RefreshProfileList());
        });
    }

    private void OnDeleteProfile()
    {
        if (_selectedProfile is null) return;
        if (_profiles.Count <= 1)
        { MessageBox.Query("Cannot Delete", "You must have at least one profile.", "OK"); return; }

        var confirm = MessageBox.Query("Delete Profile",
            $"Delete \"{_selectedProfile.ChannelTitle ?? _selectedProfile.Name}\" and all its playlists?\n\nThis cannot be undone.",
            "Delete", "Cancel");
        if (confirm != 0) return;

        var deletedId = _selectedProfile.Id;
        _ = Task.Run(async () =>
        {
            var slug = YouTubeApiServiceFactory.ToProfileSlug(_selectedProfile.Name);
            var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, slug);
            if (Directory.Exists(tokenDir)) Directory.Delete(tokenDir, recursive: true);
            await profileRepo.DeleteAsync(deletedId).ConfigureAwait(false);
            _profiles = (await profileRepo.GetAllAsync().ConfigureAwait(false)).ToList();

            var newDefault = _profiles.FirstOrDefault(p => p.IsDefault) ?? _profiles[0];
            _selectedProfile = newDefault;
            _youtubeApi = await youtubeApiFactory.CreateForProfileAsync(newDefault).ConfigureAwait(false);

            global::Terminal.Gui.Application.Invoke(() =>
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
