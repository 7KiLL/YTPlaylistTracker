
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
        Title = "";
        Width = 60;
        Height = 13;
        Border!.Settings &= ~BorderSettings.Title;

        Add(new Label { Text = " Welcome to ytpt", X = 0, Y = 0, Width = Dim.Fill(), SchemeName = Theme.SchemeFrame });
        Add(new Label() { Text = "Track YouTube playlists and detect removed videos.", X = 1, Y = 1 });
        Add(new Label() { Text = "How do you want to start?", X = 1, Y = 3 });

        var signInBtn = new Button() { Text = "Sign in with Google", X = 2, Y = 5, IsDefault = true };
        signInBtn.Accepting += (sender, e) =>
        {
            Choice = WelcomeChoice.SignIn;
            TGuiApp.RequestStop();
        };

        var offlineBtn = new Button() { Text = "Start offline", X = 26, Y = 5 };
        offlineBtn.Accepting += (sender, e) =>
        {
            Choice = WelcomeChoice.Offline;
            TGuiApp.RequestStop();
        };

        Add(signInBtn, offlineBtn);
        Add(new Label() { Text = "You can always sign in later from the profile menu.", X = 1, Y = 8, SchemeName = "Menu" });
    }
}
