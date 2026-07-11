using VintageStoryModManager.Models;

namespace VintageStoryModManager.Services;

/// <summary>
///     Pure entry-diff rules for incremental mod reloads: reload changed paths through the supplied
///     loader, reset per-load calculated state, and carry transient state (database info, search
///     score) across an entry swap when the mod identity is unchanged.
/// </summary>
internal static class ModEntryDiffHelper
{
    internal static Dictionary<string, ModEntry?> LoadChangedModEntries(
        IReadOnlyCollection<string> paths,
        IReadOnlyDictionary<string, ModEntry>? existingEntries,
        Func<string, ModEntry?> loadModFromPath)
    {
        var results = new Dictionary<string, ModEntry?>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var entry = loadModFromPath(path);
            if (entry != null)
            {
                ResetCalculatedModState(entry);
                if (existingEntries != null && existingEntries.TryGetValue(path, out var previous))
                    CopyTransientModState(previous, entry);
            }

            results[path] = entry;
        }

        return results;
    }

    internal static void ResetCalculatedModState(ModEntry entry)
    {
        entry.LoadError = null;
        entry.DependencyHasErrors = false;
        entry.MissingDependencies = Array.Empty<ModDependencyInfo>();
    }

    internal static void CopyTransientModState(ModEntry source, ModEntry target)
    {
        if (source is null || target is null) return;

        var sameModId = string.Equals(source.ModId, target.ModId, StringComparison.OrdinalIgnoreCase);
        var sameVersion = string.Equals(source.Version, target.Version, StringComparison.OrdinalIgnoreCase)
                          || (string.IsNullOrWhiteSpace(source.Version) && string.IsNullOrWhiteSpace(target.Version));

        if (!sameModId || !sameVersion) return;

        if (target.DatabaseInfo is null && source.DatabaseInfo != null) target.DatabaseInfo = source.DatabaseInfo;

        if (source.ModDatabaseSearchScore.HasValue) target.ModDatabaseSearchScore = source.ModDatabaseSearchScore;
    }
}
