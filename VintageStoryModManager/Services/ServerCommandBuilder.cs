using System.Globalization;

namespace VintageStoryModManager.Services;

/// <summary>
///     Provides utilities for building Vintage Story server commands.
/// </summary>
public static class ServerCommandBuilder
{
    private const string CommandTemplate = "/moddb install {0}@{1}";

    /// <summary>
    ///     Attempts to build a mod installation command for a Vintage Story server.
    /// </summary>
    /// <param name="modId">The unique identifier of the mod.</param>
    /// <param name="version">The version of the mod to install.</param>
    /// <returns>A server command string if both modId and version are valid; otherwise, null.</returns>
    public static string? TryBuildInstallCommand(string? modId, string? version)
    {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(version)) return null;

        var normalizedModId = modId.Trim();
        var normalizedVersion = version.Trim();
        if (normalizedModId.Length == 0 || normalizedVersion.Length == 0) return null;

        return string.Format(CultureInfo.InvariantCulture, CommandTemplate, normalizedModId, normalizedVersion);
    }

    /// <summary>
    ///     Determines whether a server install command can be copied for the given mod, given whether
    ///     server options are enabled.
    /// </summary>
    /// <param name="serverOptionsEnabled">Whether server options are enabled for the active profile.</param>
    /// <param name="modId">The unique identifier of the mod.</param>
    /// <param name="version">The version of the mod to install.</param>
    /// <returns>True if a valid install command can be built and server options are enabled; otherwise, false.</returns>
    public static bool CanCopyInstallCommand(bool serverOptionsEnabled, string? modId, string? version)
    {
        return serverOptionsEnabled && TryBuildInstallCommand(modId, version) is not null;
    }
}
