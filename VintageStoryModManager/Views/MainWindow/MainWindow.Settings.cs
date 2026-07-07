#nullable enable
using System.Windows;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void UpdateGameVersionMenuItem(string? gameVersion)
    {
        if (GameVersionMenuItem is null) return;

        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            GameVersionMenuItem.Visibility = Visibility.Collapsed;
            return;
        }

        GameVersionMenuItem.Header = $"Vintage Story: {gameVersion}";
        GameVersionMenuItem.Visibility = Visibility.Visible;
    }
}
