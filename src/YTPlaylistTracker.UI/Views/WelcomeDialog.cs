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

    public WelcomeDialog() : base()
    {
        Title = "Welcome to ytpt";
        Width = 60;
        Height = 12;
        ShadowStyle = ShadowStyle.None;
        BorderStyle = LineStyle.Rounded;

        Add(new Label() { Text = "Track YouTube playlists and detect removed videos.", X = 1, Y = 0 });
        Add(new Label() { Text = "How do you want to start?", X = 1, Y = 2 });

        var signInBtn = new Button() { Text = "Sign in with Google", X = 2, Y = 4, IsDefault = true };
        signInBtn.Accepting += (sender, e) =>
        {
            Choice = WelcomeChoice.SignIn;
            global::Terminal.Gui.Application.RequestStop();
        };

        var offlineBtn = new Button() { Text = "Start offline", X = 26, Y = 4 };
        offlineBtn.Accepting += (sender, e) =>
        {
            Choice = WelcomeChoice.Offline;
            global::Terminal.Gui.Application.RequestStop();
        };

        Add(signInBtn, offlineBtn);
        Add(new Label() { Text = "You can always sign in later from the profile menu.", X = 1, Y = 7, ColorScheme = Colors.ColorSchemes["Menu"] });
    }
}
