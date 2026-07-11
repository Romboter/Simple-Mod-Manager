using System.IO;
using VintageStoryModManager.Services;
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
}
