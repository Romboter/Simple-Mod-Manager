using System.Windows;

namespace VintageStoryModManager.Services;

/// <summary>
///     Lets a ViewModel open a dialog Window without holding a direct reference to the
///     owner window. Implemented by MainWindow, the only type that actually knows the owner.
/// </summary>
public interface IDialogLauncher
{
    bool? ShowDialog(Window dialog);
}
