using System.Threading.Tasks;

namespace VintageStoryModManager.Services
{
    /// <summary>
    ///     Service for showing confirmation dialogs in a testable, MVVM-friendly way.
    /// </summary>
    public interface IConfirmationService
    {
        Task<bool> ConfirmAsync(string message, string title);
    }
}