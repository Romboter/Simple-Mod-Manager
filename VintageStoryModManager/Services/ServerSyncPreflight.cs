#nullable enable

using System.Windows;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

internal static class ServerSyncPreflight
{
    internal static ServerSyncPreflightResult Evaluate(
        bool isServerProfile,
        string? serverTargetId,
        Func<string, ServerTarget?> getTarget,
        string? dataDirectory)
    {
        if (!isServerProfile)
            return new ServerSyncPreflightResult(null,
                "This feature is only available for Server profiles.\n\nTo use this feature, create a new profile and set its type to 'Server'.",
                MessageBoxImage.Information);

        if (string.IsNullOrEmpty(serverTargetId))
            return new ServerSyncPreflightResult(null,
                "No server target is configured for this profile.",
                MessageBoxImage.Warning);

        var target = getTarget(serverTargetId);
        if (target == null)
            return new ServerSyncPreflightResult(null,
                "The configured server target was not found. It may have been deleted.",
                MessageBoxImage.Error);

        if (string.IsNullOrWhiteSpace(dataDirectory))
            return new ServerSyncPreflightResult(null,
                "No data directory is configured for this profile.",
                MessageBoxImage.Warning);

        return new ServerSyncPreflightResult(target, null, MessageBoxImage.None);
    }

    internal static bool CanSyncToServer(bool isServerProfile, string? serverTargetId)
    {
        return isServerProfile && !string.IsNullOrEmpty(serverTargetId);
    }
}

internal readonly record struct ServerSyncPreflightResult(
    ServerTarget? Target,
    string? ErrorMessage,
    MessageBoxImage Icon);
