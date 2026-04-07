using Terminal.Gui;
using App = Terminal.Gui.Application;

namespace YTPlaylistTracker.UI;

public static class Theme
{
    public static void Apply()
    {
        static ColorScheme CreateScheme(Color normal, Color focus, Color hotNormal, Color hotFocus) => new()
        {
            Normal = App.Driver.MakeAttribute(normal, Color.Black),
            Focus = App.Driver.MakeAttribute(Color.Black, focus),
            HotNormal = App.Driver.MakeAttribute(hotNormal, Color.Black),
            HotFocus = App.Driver.MakeAttribute(Color.Black, hotFocus),
            Disabled = App.Driver.MakeAttribute(Color.DarkGray, Color.Black),
        };

        Colors.Base = CreateScheme(Color.White, Color.Cyan, Color.Cyan, Color.Cyan);
        Colors.Dialog = CreateScheme(Color.White, Color.Cyan, Color.Cyan, Color.Cyan);
        Colors.Menu = CreateScheme(Color.White, Color.Cyan, Color.Cyan, Color.Cyan);
        Colors.Error = CreateScheme(Color.Red, Color.Red, Color.BrightRed, Color.BrightRed);
    }
}
