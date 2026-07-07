namespace VintageStoryModManager.Services;

/// <summary>
///     Builds the deduplicated (mod-id, display-label) list the experimental log-debugging dialog
///     searches for. Pure input→output; twin of <see cref="InstalledModIdListBuilder" />.
/// </summary>
internal static class InstalledModLogIdentifierListBuilder
{
    internal static List<InstalledModLogIdentifier> Build(IEnumerable<(string? ModId, string? DisplayName)> mods)
    {
        var modIdentifiers = new List<InstalledModLogIdentifier>();
        var seenModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (modId, displayName) in mods)
        {
            if (string.IsNullOrWhiteSpace(modId)) continue;

            var trimmedModId = modId.Trim();
            if (!seenModIds.Add(trimmedModId)) continue;

            var trimmedDisplayName = displayName;
            if (!string.IsNullOrWhiteSpace(trimmedDisplayName)) trimmedDisplayName = trimmedDisplayName.Trim();

            var displayLabel = string.IsNullOrWhiteSpace(trimmedDisplayName)
                ? trimmedModId
                : $"{trimmedDisplayName} ({trimmedModId})";

            modIdentifiers.Add(new InstalledModLogIdentifier(trimmedModId, displayLabel));
        }

        return modIdentifiers;
    }
}
