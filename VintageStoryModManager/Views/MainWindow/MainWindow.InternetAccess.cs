#nullable enable
using System.Windows.Threading;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void InternetAccessManager_OnInternetAccessChanged(object? sender, EventArgs e)
    {
        void Update()
        {
            UpdateCloudModlistControlsEnabledState();
            _ = RefreshManagerUpdateLinkAsync();
        }

        if (Dispatcher.CheckAccess())
            Update();
        else
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)Update);
    }
}
