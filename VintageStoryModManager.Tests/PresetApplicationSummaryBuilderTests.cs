using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class PresetApplicationSummaryBuilderTests
{
    private static readonly string NewLine = Environment.NewLine;

    [Fact]
    public void BuildInstallFailureSummary_EmptyInputs_ReturnsEmptyString()
    {
        var result = PresetApplicationSummaryBuilder.BuildInstallFailureSummary(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), null, null);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BuildInstallFailureSummary_OnlyMissingMods_ReturnsMissingModsSectionOnly()
    {
        var result = PresetApplicationSummaryBuilder.BuildInstallFailureSummary(
            new[] { "ModA", "ModB" }, Array.Empty<string>(), Array.Empty<string>(), null, null);

        var expected =
            "The following mods from the preset could not be installed:" + NewLine +
            " • ModA" + NewLine +
            " • ModB";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildInstallFailureSummary_AllThreeSections_SeparatedByBlankLines()
    {
        var result = PresetApplicationSummaryBuilder.BuildInstallFailureSummary(
            new[] { "ModA" }, new[] { "ModB (1.0.0)" }, new[] { "ModC: failed" }, null, null);

        var expected =
            "The following mods from the preset could not be installed:" + NewLine +
            " • ModA" + NewLine +
            NewLine +
            "The following mod versions could not be located:" + NewLine +
            " • ModB (1.0.0)" + NewLine +
            NewLine +
            "Some mods failed to install:" + NewLine +
            " • ModC: failed";

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildInstallFailureSummary_BackupSection_RequiresMissingModsAndBackupData()
    {
        // Missing mods present, but no backup directory/names -> no backup section.
        var withoutBackupData = PresetApplicationSummaryBuilder.BuildInstallFailureSummary(
            new[] { "ModA" }, Array.Empty<string>(), Array.Empty<string>(), null, null);
        Assert.DoesNotContain("Local copies", withoutBackupData);

        // Backup data present, but no missing mods -> no backup section.
        var withoutMissingMods = PresetApplicationSummaryBuilder.BuildInstallFailureSummary(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            @"C:\backups", new[] { "ModA" });
        Assert.DoesNotContain("Local copies", withoutMissingMods);

        // Both present -> backup section appears.
        var withBoth = PresetApplicationSummaryBuilder.BuildInstallFailureSummary(
            new[] { "ModA" }, Array.Empty<string>(), Array.Empty<string>(),
            @"C:\backups", new[] { "ModA" });
        Assert.Contains("Local copies of mods that are not on the mod database were saved to:", withBoth);
        Assert.Contains(@" • C:\backups", withBoth);
    }

    [Fact]
    public void BuildInstallFailureSummary_BackupNames_AreDistinctSortedAndWhitespaceFiltered()
    {
        var result = PresetApplicationSummaryBuilder.BuildInstallFailureSummary(
            new[] { "ModA" }, Array.Empty<string>(), Array.Empty<string>(),
            @"C:\backups", new[] { "Zeta", "alpha", "ZETA", "  ", "", null!, "Alpha" });

        var expected =
            "The following mods from the preset could not be installed:" + NewLine +
            " • ModA" + NewLine +
            NewLine +
            NewLine +
            "Local copies of mods that are not on the mod database were saved to:" + NewLine +
            @" • C:\backups" + NewLine +
            "Backed up mods:" + NewLine +
            "   • alpha" + NewLine +
            "   • Zeta";

        Assert.Equal(expected, result);
    }
}
