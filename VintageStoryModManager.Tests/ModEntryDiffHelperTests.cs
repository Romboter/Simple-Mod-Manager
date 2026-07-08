using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

/// <summary>
///     Covers <see cref="ModEntryDiffHelper"/>'s pure entry-diff rules.
/// </summary>
public sealed class ModEntryDiffHelperTests
{
    private static ModEntry CreateEntry(string modId, string? version = "1.0.0", ModDatabaseInfo? databaseInfo = null,
        double? searchScore = null)
    {
        return new ModEntry
        {
            ModId = modId,
            Name = modId,
            Version = version,
            SourcePath = $@"C:\data\Mods\{modId}.zip",
            SourceKind = ModSourceKind.ZipArchive,
            DatabaseInfo = databaseInfo,
            ModDatabaseSearchScore = searchScore
        };
    }

    [Fact]
    public void LoadChangedModEntries_LoaderNull_EntryRecordedAsNull()
    {
        const string path = @"C:\data\Mods\missing.zip";

        var results = ModEntryDiffHelper.LoadChangedModEntries(
            new[] { path },
            existingEntries: null,
            loadModFromPath: _ => null);

        Assert.True(results.ContainsKey(path));
        Assert.Null(results[path]);
    }

    [Fact]
    public void LoadChangedModEntries_ResetsCalculatedState_AndCopiesTransient()
    {
        const string path = @"C:\data\Mods\mod-a.zip";
        var existingInfo = new ModDatabaseInfo { IsOfflineOnly = true };
        var previous = CreateEntry("mod-a", version: "1.0.0", databaseInfo: existingInfo);

        var freshlyLoaded = CreateEntry("mod-a", version: "1.0.0");
        freshlyLoaded.LoadError = "some load error";
        freshlyLoaded.DependencyHasErrors = true;
        freshlyLoaded.MissingDependencies = new[] { new ModDependencyInfo("dep", "1.0.0") };

        var existingEntries = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase)
        {
            [path] = previous
        };

        var results = ModEntryDiffHelper.LoadChangedModEntries(
            new[] { path },
            existingEntries,
            loadModFromPath: _ => freshlyLoaded);

        var result = results[path];
        Assert.NotNull(result);
        Assert.Null(result!.LoadError);
        Assert.False(result.DependencyHasErrors);
        Assert.Empty(result.MissingDependencies);
        Assert.Same(existingInfo, result.DatabaseInfo);
    }

    [Fact]
    public void CopyTransientModState_DifferentIdentity_NoCopy()
    {
        var info = new ModDatabaseInfo { IsOfflineOnly = true };
        var source = CreateEntry("mod-a", version: "1.0.0", databaseInfo: info);

        var differentModId = CreateEntry("mod-b", version: "1.0.0");
        ModEntryDiffHelper.CopyTransientModState(source, differentModId);
        Assert.Null(differentModId.DatabaseInfo);

        var differentVersion = CreateEntry("mod-a", version: "2.0.0");
        ModEntryDiffHelper.CopyTransientModState(source, differentVersion);
        Assert.Null(differentVersion.DatabaseInfo);

        // Both-blank versions count as the same version.
        var sourceBlankVersion = CreateEntry("mod-a", version: null, databaseInfo: info);
        var targetBlankVersion = CreateEntry("mod-a", version: null);
        ModEntryDiffHelper.CopyTransientModState(sourceBlankVersion, targetBlankVersion);
        Assert.Same(info, targetBlankVersion.DatabaseInfo);
    }

    [Fact]
    public void CopyTransientModState_TargetInfoWins()
    {
        var sourceInfo = new ModDatabaseInfo { IsOfflineOnly = true };
        var targetInfo = new ModDatabaseInfo { IsOfflineOnly = false };
        var source = CreateEntry("mod-a", databaseInfo: sourceInfo, searchScore: 0.75);
        var target = CreateEntry("mod-a", databaseInfo: targetInfo);

        ModEntryDiffHelper.CopyTransientModState(source, target);

        Assert.Same(targetInfo, target.DatabaseInfo);
        Assert.Equal(0.75, target.ModDatabaseSearchScore);

        var sourceNoScore = CreateEntry("mod-a");
        var targetWithScore = CreateEntry("mod-a", searchScore: 0.5);
        ModEntryDiffHelper.CopyTransientModState(sourceNoScore, targetWithScore);
        Assert.Equal(0.5, targetWithScore.ModDatabaseSearchScore);
    }
}
