namespace VintageStoryModManager.Services;

internal static class InstalledModIdListBuilder
{
    internal static (List<string> InstalledModIds, List<int> NumericInstalledModIds) Build(
        IEnumerable<string?> modIds)
    {
        ArgumentNullException.ThrowIfNull(modIds);

        var installedModIds = new List<string>();
        var numericInstalledModIds = new List<int>();

        foreach (var id in modIds)
        {
            if (string.IsNullOrWhiteSpace(id)) continue;

            installedModIds.Add(id);

            if (int.TryParse(id, out var modId) && !numericInstalledModIds.Contains(modId))
                numericInstalledModIds.Add(modId);
        }

        return (installedModIds, numericInstalledModIds);
    }
}
