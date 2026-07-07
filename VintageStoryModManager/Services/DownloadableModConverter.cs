#nullable enable

using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Converts <see cref="DownloadableMod" />/<see cref="DownloadableModRelease" /> data from the mod database
///     into the local <see cref="ModEntry" />/<see cref="ModReleaseInfo" /> representations.
/// </summary>
internal static class DownloadableModConverter
{
    internal static ModReleaseInfo? ToReleaseInfo(DownloadableModRelease release)
    {
        if (string.IsNullOrWhiteSpace(release.MainFile) || string.IsNullOrWhiteSpace(release.Filename))
            return null;

        // MainFile already contains the full download URL
        var downloadUri = new Uri(release.MainFile);

        // Parse the created date
        DateTime? createdUtc = null;
        if (DateTime.TryParse(release.Created, out var parsedDate))
            createdUtc = parsedDate.ToUniversalTime();

        return new ModReleaseInfo
        {
            Version = release.ModVersion,
            DownloadUri = downloadUri,
            FileName = release.Filename,
            GameVersionTags = release.Tags?.ToArray() ?? Array.Empty<string>(),
            IsCompatibleWithInstalledGame = true,
            Changelog = release.Changelog,
            Downloads = release.Downloads,
            CreatedUtc = createdUtc
        };
    }

    internal static ModEntry ToModEntry(DownloadableMod mod)
    {
        // Only consider the most recent release with a valid downloadable file
        var latestRelease = mod.Releases?
            .OrderByDescending(r => DateTime.TryParse(r.Created, out var created) ? created : DateTime.MinValue)
            .Select(ToReleaseInfo)
            .FirstOrDefault(r => r != null);

        var releases = latestRelease != null
            ? new List<ModReleaseInfo> { latestRelease }
            : new List<ModReleaseInfo>();

        // Create ModDatabaseInfo with converted data
        var logoUrlSource = !string.IsNullOrWhiteSpace(mod.LogoFileDatabase)
            ? "logofiledb"
            : null;

        var databaseInfo = new ModDatabaseInfo
        {
            Tags = mod.Tags?.ToArray() ?? Array.Empty<string>(),
            AssetId = mod.AssetId.ToString(),
            ModPageUrl = $"https://mods.vintagestory.at/show/mod/{mod.AssetId}",
            LatestVersion = latestRelease?.Version,
            LatestCompatibleVersion = latestRelease?.Version,
            RequiredGameVersions = releases.SelectMany(r => r.GameVersionTags).Distinct().ToArray(),
            Downloads = mod.Downloads,
            Comments = mod.Comments,
            Follows = mod.Follows,
            TrendingPoints = mod.TrendingPoints,
            LogoUrl = mod.LogoFileDatabase,
            LogoUrlSource = logoUrlSource,
            LastReleasedUtc = latestRelease?.CreatedUtc,
            LatestRelease = latestRelease,
            LatestCompatibleRelease = latestRelease,
            Releases = releases,
            Side = mod.Side
        };

        // Create ModEntry with the mod data
        var entry = new ModEntry
        {
            ModId = mod.ModIdStr ?? mod.ModId.ToString(),
            Name = mod.Name,
            Version = null, // Not installed yet
            Authors = !string.IsNullOrWhiteSpace(mod.Author)
                ? new[] { mod.Author }
                : Array.Empty<string>(),
            Website = mod.HomepageUrl,
            SourcePath = string.Empty,
            SourceKind = ModSourceKind.ZipArchive,
            DatabaseInfo = databaseInfo
        };

        return entry;
    }
}
