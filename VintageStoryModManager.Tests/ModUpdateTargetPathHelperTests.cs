using System.IO;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModUpdateTargetPathHelperTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("smm-upd-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void TryGetUpdateTargetPath_UsesReleaseFileNameInExistingPathDirectory()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(fileName: "testmod_2.0.0.zip"),
            @"C:\data\Mods\testmod_1.0.0.zip", out var fullPath, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(@"C:\data\Mods\testmod_2.0.0.zip", fullPath);
    }

    [Fact]
    public void TryGetUpdateTargetPath_BlankExistingPath_Fails()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(), "   ", out _, out var error);

        Assert.False(ok);
        Assert.Equal("The mod path could not be determined.", error);
    }

    [Fact]
    public void TryGetUpdateTargetPath_NoReleaseFileName_FallsBackToModIdDashVersion()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(version: "2.0.0", fileName: null),
            @"C:\data\Mods\old.zip", out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(@"C:\data\Mods\testmod-2.0.0.zip", fullPath);
    }

    [Fact]
    public void TryGetUpdateTargetPath_FileNameWithoutExtension_GetsZipAppended()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(fileName: "testmod_2"),
            @"C:\data\Mods\old.zip", out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(@"C:\data\Mods\testmod_2.zip", fullPath);
    }

    [Fact]
    public void TryGetUpdateTargetPath_FileNameWithDirectory_IsStrippedToLeafName()
    {
        var ok = ModUpdateTargetPathHelper.TryGetUpdateTargetPath(
            TestData.CreateMod(), TestData.CreateRelease(fileName: @"nested\dir\payload.zip"),
            @"C:\data\Mods\old.zip", out var fullPath, out _);

        Assert.True(ok);
        Assert.Equal(@"C:\data\Mods\payload.zip", fullPath);
    }

    [Fact]
    public void TryResolveUpdateTarget_ExistingDirectory_TargetsTheDirectoryItself()
    {
        var modDir = Directory.CreateDirectory(Path.Combine(_dir, "testmod")).FullName;

        var ok = ModUpdateTargetPathHelper.TryResolveUpdateTarget(
            TestData.CreateMod(sourceKind: ModSourceKind.Folder), TestData.CreateRelease(),
            modDir, out var targetPath, out var targetIsDirectory, out var existingPath, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.True(targetIsDirectory);
        Assert.Equal(modDir, targetPath);
        Assert.Null(existingPath);
    }

    [Fact]
    public void TryResolveUpdateTarget_MissingPathForFolderMod_TreatedAsDirectory()
    {
        var missing = Path.Combine(_dir, "gone");

        var ok = ModUpdateTargetPathHelper.TryResolveUpdateTarget(
            TestData.CreateMod(sourceKind: ModSourceKind.Folder), TestData.CreateRelease(),
            missing, out var targetPath, out var targetIsDirectory, out _, out _);

        Assert.True(ok);
        Assert.True(targetIsDirectory);
        Assert.Equal(missing, targetPath);
    }

    [Fact]
    public void TryResolveUpdateTarget_ExistingFile_ResolvesNewPathBesideIt()
    {
        var existing = Path.Combine(_dir, "testmod_1.0.0.zip");
        File.WriteAllText(existing, "");

        var ok = ModUpdateTargetPathHelper.TryResolveUpdateTarget(
            TestData.CreateMod(), TestData.CreateRelease(fileName: "testmod_2.0.0.zip"),
            existing, out var targetPath, out var targetIsDirectory, out var existingPath, out _);

        Assert.True(ok);
        Assert.False(targetIsDirectory);
        Assert.Equal(Path.Combine(_dir, "testmod_2.0.0.zip"), targetPath);
        Assert.Equal(existing, existingPath);
    }
}
