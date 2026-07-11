#nullable enable

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    public void ReportStatus(string message, bool isError = false)
    {
        _viewModel?.ReportStatus(message, isError);
    }
}
