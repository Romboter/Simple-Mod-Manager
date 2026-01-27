using System.Threading.Tasks;
using System.Windows;

namespace VintageStoryModManager.Services
{
    /// <summary>
    ///     Default implementation of IConfirmationService using MessageBox.
    /// </summary>
    public class ConfirmationService : IConfirmationService
    {
        public Task<bool> ConfirmAsync(string message, string title)
        {
            var dialog = new VintageStoryModManager.Views.Dialogs.ThemedConfirmationDialog(message, title)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            var result = dialog.ShowDialog();
            return Task.FromResult(result == true && dialog.UserConfirmed);
        }
    }
}