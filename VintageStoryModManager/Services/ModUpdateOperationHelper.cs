#nullable enable

namespace VintageStoryModManager.Services;

internal static class ModUpdateOperationHelper
{
    internal static async Task<ModUpdateOperationOutcome> ExecuteAsync(
        ModUpdateService modUpdateService,
        ModUpdateDescriptor descriptor,
        bool cacheDownloads,
        IProgress<ModUpdateProgress>? progress,
        string failureFallbackMessage)
    {
        var result = await modUpdateService
            .UpdateAsync(descriptor, cacheDownloads, progress)
            .ConfigureAwait(false);

        if (result.Success) return new ModUpdateOperationOutcome(true, null);

        var message = string.IsNullOrWhiteSpace(result.ErrorMessage) ? failureFallbackMessage : result.ErrorMessage;
        return new ModUpdateOperationOutcome(false, message);
    }
}

internal readonly record struct ModUpdateOperationOutcome(bool Success, string? ErrorMessage);
