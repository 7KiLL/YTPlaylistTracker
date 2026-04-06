using Terminal.Gui;
using App = Terminal.Gui.Application;

namespace YTPlaylistTracker.UI;

public static class Theme
{
    public static void Apply()
    {
        // Modern dark theme — black background, cyan accents (lazydocker-style)
        var baseScheme = new ColorScheme
        {
            Normal = App.Driver.MakeAttribute(Color.White, Color.Black),
            Focus = App.Driver.MakeAttribute(Color.Black, Color.Cyan),
            HotNormal = App.Driver.MakeAttribute(Color.Cyan, Color.Black),
            HotFocus = App.Driver.MakeAttribute(Color.Black, Color.Cyan),
            Disabled = App.Driver.MakeAttribute(Color.DarkGray, Color.Black),
        };

        var dialogScheme = new ColorScheme
        {
            Normal = App.Driver.MakeAttribute(Color.White, Color.Black),
            Focus = App.Driver.MakeAttribute(Color.Black, Color.Cyan),
            HotNormal = App.Driver.MakeAttribute(Color.Cyan, Color.Black),
            HotFocus = App.Driver.MakeAttribute(Color.Black, Color.Cyan),
            Disabled = App.Driver.MakeAttribute(Color.DarkGray, Color.Black),
        };

        var menuScheme = new ColorScheme
        {
            Normal = App.Driver.MakeAttribute(Color.White, Color.Black),
            Focus = App.Driver.MakeAttribute(Color.Black, Color.Cyan),
            HotNormal = App.Driver.MakeAttribute(Color.Cyan, Color.Black),
            HotFocus = App.Driver.MakeAttribute(Color.Black, Color.Cyan),
            Disabled = App.Driver.MakeAttribute(Color.DarkGray, Color.Black),
        };

        var errorScheme = new ColorScheme
        {
            Normal = App.Driver.MakeAttribute(Color.Red, Color.Black),
            Focus = App.Driver.MakeAttribute(Color.Black, Color.Red),
            HotNormal = App.Driver.MakeAttribute(Color.BrightRed, Color.Black),
            HotFocus = App.Driver.MakeAttribute(Color.Black, Color.BrightRed),
            Disabled = App.Driver.MakeAttribute(Color.DarkGray, Color.Black),
        };

        Colors.Base = baseScheme;
        Colors.Dialog = dialogScheme;
        Colors.Menu = menuScheme;
        Colors.Error = errorScheme;
    }
}
