using System.IO;
using System.Text.Json;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModlistWorkflowServiceTests
{
    [Fact]
    public void ResolveModlistFilePath_PrefersListNameOverSuggestedName()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var result = ModlistWorkflowService.ResolveModlistFilePath(directory, "My List", "Fallback", ".json");

            Assert.Equal(Path.Combine(directory, "My List.json"), result.FilePath);
            Assert.Equal("My List", result.EntryName);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ResolveModlistFilePath_FallsBackToSuggestedName_WhenListNameBlank()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var result = ModlistWorkflowService.ResolveModlistFilePath(directory, "   ", "Fallback", ".json");

            Assert.Equal(Path.Combine(directory, "Fallback.json"), result.FilePath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ResolveModlistFilePath_ExistingFile_ReportsAlreadyExistsTrue()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "My List.json"), "{}");

            var result = ModlistWorkflowService.ResolveModlistFilePath(directory, "My List", null, ".json");

            Assert.True(result.AlreadyExists);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ResolveModlistFilePath_NoFile_ReportsAlreadyExistsFalse()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var result = ModlistWorkflowService.ResolveModlistFilePath(directory, "New List", null, ".pdf");

            Assert.False(result.AlreadyExists);
            Assert.Equal(Path.Combine(directory, "New List.pdf"), result.FilePath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void BuildModlistJson_IncludesNameAndUploader()
    {
        var modStates = new[] { new ModPresetModState("mod1", "1.0.0", true, null, null) };

        var json = ModlistWorkflowService.BuildModlistJson(
            modStates,
            "My List",
            description: null,
            version: null,
            uploader: "Tester",
            includedConfigurations: null,
            gameVersion: "1.20.0");

        using var document = JsonDocument.Parse(json);
        Assert.Equal("My List", document.RootElement.GetProperty("name").GetString());
        Assert.Equal("Tester", document.RootElement.GetProperty("uploader").GetString());
    }

    [Fact]
    public void SaveJsonModlist_WritesFileAndReturnsSuccess()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var filePath = Path.Combine(directory, "My List.json");
            var modStates = new[] { new ModPresetModState("mod1", "1.0.0", true, null, null) };

            var result = ModlistWorkflowService.SaveJsonModlist(
                filePath, modStates, "My List", null, null, "Tester", null, "1.20.0");

            Assert.True(result.Success);
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void SavePdfModlist_WritesFile()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var filePath = Path.Combine(directory, "My List.pdf");
            var modStates = new[] { new ModPresetModState("mod1", "1.0.0", true, null, null) };

            ModlistWorkflowService.SavePdfModlist(
                filePath,
                "My List",
                version: null,
                description: null,
                uploaderName: "Tester",
                gameVersion: "1.20.0",
                mods: Array.Empty<ModListItemViewModel>(),
                modStates: modStates,
                includedConfigurations: null);

            Assert.True(File.Exists(filePath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
