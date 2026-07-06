using System.IO;
using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class LocalModlistFileServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("smm-test-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void Save_WritesJsonThatRoundTrips()
    {
        var preset = new SerializablePreset { Name = "Test", Mods = [] };
        var path = Path.Combine(_dir, "modlist.json");

        var result = LocalModlistFileService.Save(path, preset);

        Assert.True(result.Success);

        var json = File.ReadAllText(path);
        Assert.True(PdfModlistSerializer.TryDeserializeFromJson(json, out var deserialized, out _));
        Assert.Equal("Test", deserialized!.Name);
    }

    [Fact]
    public void Save_ReturnsFailureForMissingDirectory()
    {
        var preset = new SerializablePreset { Name = "Test", Mods = [] };
        var path = Path.Combine(_dir, "missing-subdir", "modlist.json");

        var result = LocalModlistFileService.Save(path, preset);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }
}
