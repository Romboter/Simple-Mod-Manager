using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Builds offline <see cref="ModDatabaseInfo" /> for installed mods when no online data is
///     available: synthesizes releases from the mod-cache archives (zip/JSON parsing via
///     <see cref="ModCacheLocator" />), determines installed-game compatibility, and merges
///     offline-synthesized info with previously cached online info. Pure logic — no WPF, no
///     dispatcher, no shared mutable state beyond the two injected reads.
/// </summary>
public sealed class OfflineModDatabaseInfoBuilder
{
    private readonly Func<string?> _installedGameVersionProvider;
    private readonly Func<bool> _requireExactVsVersionMatch;

    public OfflineModDatabaseInfoBuilder(
        Func<string?> installedGameVersionProvider,
        Func<bool> requireExactVsVersionMatch)
    {
        _installedGameVersionProvider = installedGameVersionProvider;
        _requireExactVsVersionMatch = requireExactVsVersionMatch;
    }

    public ModDatabaseInfo? CreateOfflineDatabaseInfo(ModEntry entry)
    {
        if (entry is null) return null;

        var dependencies = entry.Dependencies ?? Array.Empty<ModDependencyInfo>();
        var installedRequiredGameVersions = ExtractRequiredGameVersions(dependencies);
        var releases = CreateOfflineReleases(entry, installedRequiredGameVersions, dependencies);
        var aggregatedRequiredGameVersions = AggregateRequiredGameVersions(installedRequiredGameVersions, releases);

        var latestRelease = releases.Count > 0 ? releases[0] : null;
        var latestCompatibleRelease = releases.FirstOrDefault(release => release.IsCompatibleWithInstalledGame);
        var lastUpdatedUtc = DetermineOfflineLastUpdatedUtc(entry, releases);

        var latestVersion = latestRelease?.Version ?? entry.Version;
        var latestCompatibleVersion = latestCompatibleRelease?.Version ?? entry.Version;

        return new ModDatabaseInfo
        {
            RequiredGameVersions = aggregatedRequiredGameVersions,
            LatestVersion = latestVersion,
            LatestCompatibleVersion = latestCompatibleVersion,
            LatestRelease = latestRelease,
            LatestCompatibleRelease = latestCompatibleRelease ?? latestRelease,
            Releases = releases,
            LastReleasedUtc = lastUpdatedUtc,
            IsOfflineOnly = true,
            Side = entry.Side
        };
    }

    public static ModDatabaseInfo CreateInfoWithoutTags(ModDatabaseInfo source)
    {
        return new ModDatabaseInfo
        {
            Tags = Array.Empty<string>(),
            CachedTagsVersion = null,
            AssetId = source.AssetId,
            ModPageUrl = source.ModPageUrl,
            LatestCompatibleVersion = source.LatestCompatibleVersion,
            LatestVersion = source.LatestVersion,
            RequiredGameVersions = source.RequiredGameVersions,
            Downloads = source.Downloads,
            Comments = source.Comments,
            Follows = source.Follows,
            TrendingPoints = source.TrendingPoints,
            LogoUrl = source.LogoUrl,
            LogoUrlSource = source.LogoUrlSource,
            DownloadsLastThirtyDays = source.DownloadsLastThirtyDays,
            DownloadsLastTenDays = source.DownloadsLastTenDays,
            LastReleasedUtc = source.LastReleasedUtc,
            CreatedUtc = source.CreatedUtc,
            LatestRelease = source.LatestRelease,
            LatestCompatibleRelease = source.LatestCompatibleRelease,
            Releases = source.Releases,
            IsOfflineOnly = source.IsOfflineOnly,
            Side = source.Side
        };
    }

    public static ModDatabaseInfo? MergeOfflineAndCachedInfo(ModDatabaseInfo? offlineInfo, ModDatabaseInfo? cachedInfo)
    {
        if (offlineInfo is null) return cachedInfo;

        if (cachedInfo is null) return offlineInfo;

        var mergedReleases = MergeReleases(offlineInfo.Releases, cachedInfo.Releases);
        var latestRelease = offlineInfo.LatestRelease ?? cachedInfo.LatestRelease;
        if (latestRelease is null && mergedReleases is { Count: > 0 }) latestRelease = mergedReleases[0];

        var latestCompatibleRelease = offlineInfo.LatestCompatibleRelease ?? cachedInfo.LatestCompatibleRelease;
        if (latestCompatibleRelease is null && mergedReleases is { Count: > 0 })
            latestCompatibleRelease =
                mergedReleases.FirstOrDefault(release => release?.IsCompatibleWithInstalledGame == true);

        var requiredVersions = offlineInfo.RequiredGameVersions is { Count: > 0 }
            ? offlineInfo.RequiredGameVersions
            : cachedInfo.RequiredGameVersions;

        return new ModDatabaseInfo
        {
            Tags = cachedInfo.Tags ?? offlineInfo.Tags ?? Array.Empty<string>(),
            CachedTagsVersion = cachedInfo.CachedTagsVersion ?? offlineInfo.CachedTagsVersion,
            AssetId = cachedInfo.AssetId ?? offlineInfo.AssetId,
            ModPageUrl = cachedInfo.ModPageUrl ?? offlineInfo.ModPageUrl,
            LatestCompatibleVersion = offlineInfo.LatestCompatibleVersion ?? cachedInfo.LatestCompatibleVersion,
            LatestVersion = offlineInfo.LatestVersion ?? cachedInfo.LatestVersion,
            RequiredGameVersions = requiredVersions,
            Downloads = cachedInfo.Downloads ?? offlineInfo.Downloads,
            Comments = cachedInfo.Comments ?? offlineInfo.Comments,
            Follows = cachedInfo.Follows ?? offlineInfo.Follows,
            TrendingPoints = cachedInfo.TrendingPoints ?? offlineInfo.TrendingPoints,
            LogoUrl = cachedInfo.LogoUrl ?? offlineInfo.LogoUrl,
            LogoUrlSource = cachedInfo.LogoUrlSource ?? offlineInfo.LogoUrlSource,
            DownloadsLastThirtyDays = cachedInfo.DownloadsLastThirtyDays ?? offlineInfo.DownloadsLastThirtyDays,
            LastReleasedUtc = offlineInfo.LastReleasedUtc ?? cachedInfo.LastReleasedUtc,
            CreatedUtc = cachedInfo.CreatedUtc ?? offlineInfo.CreatedUtc,
            LatestRelease = latestRelease,
            LatestCompatibleRelease = latestCompatibleRelease ?? latestRelease,
            Releases = mergedReleases,
            IsOfflineOnly = offlineInfo.IsOfflineOnly && (cachedInfo?.IsOfflineOnly ?? true),
            Side = cachedInfo?.Side ?? offlineInfo.Side
        };
    }

    private static IReadOnlyList<ModReleaseInfo> MergeReleases(
        IReadOnlyList<ModReleaseInfo>? offlineReleases,
        IReadOnlyList<ModReleaseInfo>? cachedReleases)
    {
        if (offlineReleases is not { Count: > 0 })
            return cachedReleases is { Count: > 0 } ? cachedReleases : Array.Empty<ModReleaseInfo>();

        if (cachedReleases is not { Count: > 0 }) return offlineReleases;

        var byVersion = new Dictionary<string, ModReleaseInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var release in cachedReleases)
        {
            if (release is null || string.IsNullOrWhiteSpace(release.Version)) continue;

            if (!byVersion.ContainsKey(release.Version)) byVersion[release.Version] = release;
        }

        foreach (var release in offlineReleases)
        {
            if (release is null || string.IsNullOrWhiteSpace(release.Version)) continue;

            byVersion[release.Version] = release;
        }

        if (byVersion.Count == 0) return Array.Empty<ModReleaseInfo>();

        var merged = byVersion.Values.ToList();
        merged.Sort(CompareOfflineReleases);
        return merged;
    }

    private IReadOnlyList<ModReleaseInfo> CreateOfflineReleases(
        ModEntry entry,
        IReadOnlyList<string> installedRequiredGameVersions,
        IReadOnlyList<ModDependencyInfo> installedDependencies)
    {
        var releases = new List<ModReleaseInfo>();
        var seenVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var installedRelease = CreateOfflineRelease(entry, installedRequiredGameVersions, installedDependencies);
        if (installedRelease != null)
        {
            releases.Add(installedRelease);
            seenVersions.Add(installedRelease.Version);
        }

        foreach (var cachedRelease in EnumerateCachedModReleases(entry.ModId))
        {
            if (!seenVersions.Add(cachedRelease.Version)) continue;

            releases.Add(cachedRelease);
        }

        if (releases.Count == 0) return Array.Empty<ModReleaseInfo>();

        releases.Sort(CompareOfflineReleases);
        return releases.AsReadOnly();
    }

    private ModReleaseInfo? CreateOfflineRelease(
        ModEntry entry,
        IReadOnlyList<string> requiredGameVersions,
        IReadOnlyList<ModDependencyInfo> dependencies)
    {
        if (string.IsNullOrWhiteSpace(entry.Version)) return null;

        if (!TryCreateFileUri(entry.SourcePath, entry.SourceKind, out var downloadUri)) return null;

        var createdUtc = TryGetLastWriteTimeUtc(entry.SourcePath, entry.SourceKind);

        return new ModReleaseInfo
        {
            Version = entry.Version!,
            NormalizedVersion = VersionStringUtility.Normalize(entry.Version),
            DownloadUri = downloadUri!,
            FileName = TryGetReleaseFileName(entry.SourcePath),
            GameVersionTags = requiredGameVersions,
            IsCompatibleWithInstalledGame = DetermineInstalledGameCompatibility(dependencies),
            CreatedUtc = createdUtc
        };
    }

    private IEnumerable<ModReleaseInfo> EnumerateCachedModReleases(string modId)
    {
        var cacheDirectory = ModCacheLocator.GetModCacheDirectory(modId);
        if (string.IsNullOrWhiteSpace(cacheDirectory)) yield break;

        try
        {
            if (!Directory.Exists(cacheDirectory)) yield break;
        }
        catch (Exception)
        {
            yield break;
        }

        foreach (var file in ModCacheLocator.EnumerateCachedFiles(modId))
        {
            var release = TryCreateCachedRelease(file, modId);
            if (release != null) yield return release;
        }
    }

    private ModReleaseInfo? TryCreateCachedRelease(string archivePath, string expectedModId)
    {
        var lastWriteTimeUtc = DateTime.MinValue;
        var length = 0L;

        try
        {
            var fileInfo = new FileInfo(archivePath);
            if (fileInfo.Exists)
            {
                lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
                length = fileInfo.Length;
            }
        }
        catch (Exception)
        {
            // Ignore filesystem probing failures; the cache simply will not be used.
        }

        if (ModManifestCacheService.TryGetManifest(archivePath, lastWriteTimeUtc, length, out var cachedManifest,
                out _))
            try
            {
                using var document = JsonDocument.Parse(cachedManifest);
                var root = document.RootElement;

                var modId = GetString(root, "modid") ?? GetString(root, "modID");
                if (!IsModIdMatch(expectedModId, modId))
                {
                    ModManifestCacheService.Invalidate(archivePath);
                }
                else
                {
                    var version = GetString(root, "version") ?? TryResolveVersionFromMap(root);
                    if (string.IsNullOrWhiteSpace(version)) return null;

                    var dependencies = ParseDependencies(root);
                    var requiredGameVersions = ExtractRequiredGameVersions(dependencies);

                    if (!TryCreateFileUri(archivePath, ModSourceKind.ZipArchive, out var downloadUri)) return null;

                    var createdUtc = TryGetLastWriteTimeUtc(archivePath, ModSourceKind.ZipArchive);
                    return new ModReleaseInfo
                    {
                        Version = version!,
                        NormalizedVersion = VersionStringUtility.Normalize(version),
                        DownloadUri = downloadUri!,
                        FileName = Path.GetFileName(archivePath),
                        GameVersionTags = requiredGameVersions,
                        IsCompatibleWithInstalledGame = DetermineInstalledGameCompatibility(dependencies),
                        CreatedUtc = createdUtc
                    };
                }
            }
            catch (Exception)
            {
                ModManifestCacheService.Invalidate(archivePath);
            }

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var infoEntry = FindArchiveEntry(archive, "modinfo.json");
            if (infoEntry == null) return null;

            string manifestContent;
            using (var infoStream = infoEntry.Open())
            using (var reader = new StreamReader(infoStream, Encoding.UTF8, true))
            {
                manifestContent = reader.ReadToEnd();
            }

            using var document = JsonDocument.Parse(manifestContent);
            var root = document.RootElement;

            var modId = GetString(root, "modid") ?? GetString(root, "modID");
            if (!IsModIdMatch(expectedModId, modId)) return null;

            var version = GetString(root, "version") ?? TryResolveVersionFromMap(root);
            if (string.IsNullOrWhiteSpace(version)) return null;

            var dependencies = ParseDependencies(root);
            var requiredGameVersions = ExtractRequiredGameVersions(dependencies);

            if (!TryCreateFileUri(archivePath, ModSourceKind.ZipArchive, out var downloadUri)) return null;

            var createdUtc = TryGetLastWriteTimeUtc(archivePath, ModSourceKind.ZipArchive);

            var cacheModId = !string.IsNullOrWhiteSpace(modId)
                ? modId
                : !string.IsNullOrWhiteSpace(expectedModId)
                    ? expectedModId
                    : Path.GetFileNameWithoutExtension(archivePath);

            ModManifestCacheService.StoreManifest(archivePath, lastWriteTimeUtc, length, cacheModId, version,
                manifestContent, null);

            return new ModReleaseInfo
            {
                Version = version!,
                NormalizedVersion = VersionStringUtility.Normalize(version),
                DownloadUri = downloadUri!,
                FileName = Path.GetFileName(archivePath),
                GameVersionTags = requiredGameVersions,
                IsCompatibleWithInstalledGame = DetermineInstalledGameCompatibility(dependencies),
                CreatedUtc = createdUtc
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsModIdMatch(string expectedModId, string? actualModId)
    {
        if (string.IsNullOrWhiteSpace(expectedModId) || string.IsNullOrWhiteSpace(actualModId)) return true;

        return string.Equals(actualModId, expectedModId, StringComparison.OrdinalIgnoreCase);
    }

    private static ZipArchiveEntry? FindArchiveEntry(ZipArchive archive, string entryName)
    {
        foreach (var entry in archive.Entries)
            if (string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase))
                return entry;

        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(Path.GetFileName(entry.FullName), entryName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }

        value = default;
        return false;
    }

    private static string? TryResolveVersionFromMap(JsonElement root)
    {
        if (!TryGetProperty(root, "versionmap", out var map) &&
            !TryGetProperty(root, "VersionMap", out map)) return null;

        if (map.ValueKind != JsonValueKind.Object) return null;

        string? preferred = null;
        string? fallback = null;
        foreach (var property in map.EnumerateObject())
        {
            var version = property.Value.GetString();
            if (version == null) continue;

            fallback = version;
            if (property.Name.Contains("1.21", StringComparison.OrdinalIgnoreCase)) preferred = version;
        }

        return preferred ?? fallback;
    }

    private static IReadOnlyList<ModDependencyInfo> ParseDependencies(JsonElement root)
    {
        if (!TryGetProperty(root, "dependencies", out var dependenciesElement)
            || dependenciesElement.ValueKind != JsonValueKind.Object)
            return Array.Empty<ModDependencyInfo>();

        var dependencies = new List<ModDependencyInfo>();
        foreach (var property in dependenciesElement.EnumerateObject())
        {
            var version = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString() ?? string.Empty
                : string.Empty;
            dependencies.Add(new ModDependencyInfo(property.Name, version));
        }

        return dependencies.Count == 0 ? Array.Empty<ModDependencyInfo>() : dependencies;
    }

    private static IReadOnlyList<string> AggregateRequiredGameVersions(
        IReadOnlyList<string> installedRequiredGameVersions,
        IReadOnlyList<ModReleaseInfo> releases)
    {
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var version in installedRequiredGameVersions)
            if (!string.IsNullOrWhiteSpace(version))
                versions.Add(version);

        foreach (var release in releases)
        {
            if (release?.GameVersionTags is null) continue;

            foreach (var tag in release.GameVersionTags)
                if (!string.IsNullOrWhiteSpace(tag))
                    versions.Add(tag);
        }

        return versions.Count == 0 ? Array.Empty<string>() : versions.ToArray();
    }

    private static DateTime? DetermineOfflineLastUpdatedUtc(ModEntry entry, IReadOnlyList<ModReleaseInfo> releases)
    {
        DateTime? lastUpdatedUtc = null;
        foreach (var release in releases)
        {
            if (release?.CreatedUtc is not DateTime created) continue;

            if (!lastUpdatedUtc.HasValue || created > lastUpdatedUtc) lastUpdatedUtc = created;
        }

        return lastUpdatedUtc ?? TryGetLastWriteTimeUtc(entry.SourcePath, entry.SourceKind);
    }

    private static int CompareOfflineReleases(ModReleaseInfo? left, ModReleaseInfo? right)
    {
        if (ReferenceEquals(left, right)) return 0;

        if (left is null) return 1;

        if (right is null) return -1;

        if (VersionStringUtility.IsCandidateVersionNewer(left.Version, right.Version)) return -1;

        if (VersionStringUtility.IsCandidateVersionNewer(right.Version, left.Version)) return 1;

        var leftTimestamp = left.CreatedUtc ?? DateTime.MinValue;
        var rightTimestamp = right.CreatedUtc ?? DateTime.MinValue;
        var dateComparison = rightTimestamp.CompareTo(leftTimestamp);
        if (dateComparison != 0) return dateComparison;

        return string.Compare(left.Version, right.Version, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractRequiredGameVersions(IReadOnlyList<ModDependencyInfo> dependencies)
    {
        if (dependencies is null || dependencies.Count == 0) return Array.Empty<string>();

        var versions = dependencies
            .Where(dependency => dependency != null && dependency.IsGameOrCoreDependency)
            .Select(dependency => dependency.Version?.Trim())
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Select(version => version!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return versions.Length == 0 ? Array.Empty<string>() : versions;
    }

    internal bool DetermineInstalledGameCompatibility(IReadOnlyList<ModDependencyInfo> dependencies)
    {
        if (dependencies is null || dependencies.Count == 0) return true;

        var installedGameVersion = _installedGameVersionProvider();
        if (string.IsNullOrWhiteSpace(installedGameVersion)) return true;

        foreach (var dependency in dependencies)
        {
            if (dependency is null || !dependency.IsGameOrCoreDependency) continue;

            // Check if the installed game version satisfies the dependency's minimum version requirement
            // (e.g., if mod requires 1.21.0 and user has 1.21.4, that's OK as 1.21.4 >= 1.21.0)
            if (!VersionStringUtility.SatisfiesMinimumVersion(dependency.Version, installedGameVersion)) return false;

            // When exact version match is required, also verify first 3 version parts match
            // (e.g., with exact mode, 1.21.3 won't be compatible with 1.21.4 even though 1.21.4 >= 1.21.3)
            if (_requireExactVsVersionMatch())
                if (!VersionStringUtility.MatchesFirstThreeDigits(dependency.Version, installedGameVersion))
                    return false;
        }

        return true;
    }

    private static bool TryCreateFileUri(string? path, ModSourceKind kind, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (kind == ModSourceKind.Folder)
                fullPath = Path.TrimEndingDirectorySeparator(fullPath) + Path.DirectorySeparatorChar;

            return Uri.TryCreate(fullPath, UriKind.Absolute, out uri);
        }
        catch (Exception)
        {
            uri = null;
            return false;
        }
    }

    private static string? TryGetReleaseFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            var normalized = Path.GetFullPath(path);
            if (Directory.Exists(normalized)) normalized = Path.TrimEndingDirectorySeparator(normalized);

            var fileName = Path.GetFileName(normalized);
            return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static DateTime? TryGetLastWriteTimeUtc(string? path, ModSourceKind kind)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            return kind == ModSourceKind.Folder
                ? Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path) : null
                : File.Exists(path)
                    ? File.GetLastWriteTimeUtc(path)
                    : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
