using System.IO;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ManagedModPathHelperTests : IDisposable
{
    private readonly string _dataDir = Directory.CreateTempSubdirectory("smm-data-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dataDir, true);
    }

    [Fact]
    public void IsPathWithinManagedMods_AcceptsModsAndModsByServer()
    {
        Assert.True(ManagedModPathHelper.IsPathWithinManagedMods(
            _dataDir, Path.Combine(_dataDir, "Mods", "m.zip")));
        Assert.True(ManagedModPathHelper.IsPathWithinManagedMods(
            _dataDir, Path.Combine(_dataDir, "ModsByServer", "srv", "m.zip")));
    }

    [Fact]
    public void IsPathWithinManagedMods_RejectsOutsidersAndNullDataDir()
    {
        Assert.False(ManagedModPathHelper.IsPathWithinManagedMods(
            _dataDir, Path.Combine(_dataDir, "Saves", "m.zip")));
        Assert.False(ManagedModPathHelper.IsPathWithinManagedMods(
            null, Path.Combine(_dataDir, "Mods", "m.zip")));
    }

    [Fact]
    public void TryEnsureManagedModTargetIsSafe_NonExistentPath_IsSafe()
    {
        var ok = ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(
            _dataDir, Path.Combine(_dataDir, "Mods", "missing.zip"), out var error);
        Assert.True(ok);
        Assert.Null(error);
    }

    [Fact]
    public void TryEnsureManagedModTargetIsSafe_RegularFileAndDirectory_AreSafe()
    {
        var modsDir = Directory.CreateDirectory(Path.Combine(_dataDir, "Mods")).FullName;
        var file = Path.Combine(modsDir, "m.zip");
        File.WriteAllText(file, "");

        Assert.True(ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(_dataDir, file, out var fileError));
        Assert.Null(fileError);
        Assert.True(ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(_dataDir, modsDir, out var dirError));
        Assert.Null(dirError);
    }

    [Fact]
    public void TryEnsureManagedModTargetIsSafe_SymlinkOutsideManagedMods_IsRejected()
    {
        var modsDir = Directory.CreateDirectory(Path.Combine(_dataDir, "Mods")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(_dataDir, "Outside")).FullName;
        var link = Path.Combine(modsDir, "linked");
        if (!TryCreateSymlink(link, outside)) return; // needs Developer Mode/admin; skip when unavailable

        var ok = ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(_dataDir, link, out var error);
        Assert.False(ok);
        Assert.Contains("points outside", error);
    }

    [Fact]
    public void TryEnsureManagedModTargetIsSafe_SymlinkInsideManagedMods_IsSafe()
    {
        var modsDir = Directory.CreateDirectory(Path.Combine(_dataDir, "Mods")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(modsDir, "real")).FullName;
        var link = Path.Combine(modsDir, "linked");
        if (!TryCreateSymlink(link, target)) return; // needs Developer Mode/admin; skip when unavailable

        var ok = ManagedModPathHelper.TryEnsureManagedModTargetIsSafe(_dataDir, link, out var error);
        Assert.True(ok);
        Assert.Null(error);
    }

    private static bool TryCreateSymlink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
