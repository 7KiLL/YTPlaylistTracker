using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public enum WelcomeChoice
{
    SignIn,
    Offline,
}

public sealed class WelcomeDialog : Dialog
{
    public WelcomeChoice Choice { get; private set; } = WelcomeChoice.Offline;

    public WelcomeDialog() : base("Welcome to ytpt", 60, 12)
    {
        Add(new Label("Track YouTube playlists and detect removed videos.") { X = 1, Y = 0 });
        Add(new Label("How do you want to start?") { X = 1, Y = 2 });

        var signInBtn = new Button("Sign in with Google") { X = 2, Y = 4, IsDefault = true };
        signInBtn.Clicked += () =>
        {
            Choice = WelcomeChoice.SignIn;
            global::Terminal.Gui.Application.RequestStop();
        };

        var offlineBtn = new Button("Start offline") { X = 26, Y = 4 };
        offlineBtn.Clicked += () =>
        {
            Choice = WelcomeChoice.Offline;
            global::Terminal.Gui.Application.RequestStop();
        };

        Add(signInBtn, offlineBtn);
        Add(new Label("You can always sign in later from the profile menu.") { X = 1, Y = 7, ColorScheme = Colors.Menu });
    }
}
