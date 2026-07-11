using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Tests;

internal static class TestData
{
    internal static ModListItemViewModel CreateMod(
        string modId = "testmod",
        string sourcePath = @"C:\data\Mods\testmod.zip",
        ModSourceKind sourceKind = ModSourceKind.ZipArchive,
        string location = "Mods")
    {
        var entry = new ModEntry
        {
            ModId = modId,
            Name = modId,
            SourcePath = sourcePath,
            SourceKind = sourceKind
        };

        return new ModListItemViewModel(
            entry,
            false,
            location,
            (_, _) => Task.FromResult<ActivationResult>(default!),
            initializeUserReportState: false);
    }

    internal static ServerTarget CreateServerTarget(string id = "target-1")
    {
        return new ServerTarget
        {
            Id = id,
            Name = "Test Server",
            Host = "example.invalid",
            Username = "tester",
            RemoteDataPath = "/srv/vintagestory/data"
        };
    }

    internal static ModReleaseInfo CreateRelease(
        string version = "1.2.3",
        string? fileName = "testmod_1.2.3.zip")
    {
        return new ModReleaseInfo
        {
            Version = version,
            DownloadUri = new Uri("https://example.invalid/testmod.zip"),
            FileName = fileName
        };
    }
}
