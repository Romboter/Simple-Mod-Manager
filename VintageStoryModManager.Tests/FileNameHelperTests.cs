using System.IO;
using VintageStoryModManager.Helpers;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class FileNameHelperTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("smm-test-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, true);
    }

    [Theory]
    [InlineData("mod:1.zip", "fallback.zip", "mod_1.zip")] // ':' invalid on Windows
    [InlineData("a<b>c.zip", "fallback.zip", "a_b_c.zip")]
    [InlineData(null, "fallback.zip", "fallback.zip")]
    [InlineData("   ", "fallback.zip", "fallback.zip")]
    [InlineData("normal.zip", "fallback.zip", "normal.zip")]
    [InlineData("  padded.zip  ", "fallback.zip", "padded.zip")] // trimmed
    public void SanitizeFileName_ReplacesInvalidCharsOrFallsBack(string? name, string fallback, string expected)
    {
        Assert.Equal(expected, FileNameHelper.SanitizeFileName(name, fallback));
    }

    [Fact]
    public void EnsureUniqueFilePath_ReturnsOriginalWhenFree()
    {
        var path = Path.Combine(_dir, "mod.zip");
        Assert.Equal(path, FileNameHelper.EnsureUniqueFilePath(path));
    }

    [Fact]
    public void EnsureUniqueFilePath_AppendsCounterWhenTaken()
    {
        var path = Path.Combine(_dir, "mod.zip");
        File.WriteAllText(path, "");
        Assert.Equal(Path.Combine(_dir, "mod (1).zip"), FileNameHelper.EnsureUniqueFilePath(path));

        File.WriteAllText(Path.Combine(_dir, "mod (1).zip"), "");
        Assert.Equal(Path.Combine(_dir, "mod (2).zip"), FileNameHelper.EnsureUniqueFilePath(path));
    }

    [Fact]
    public void EnsureUniqueDirectoryPath_AppendsCounterWhenTaken()
    {
        var path = Path.Combine(_dir, "backup");
        Assert.Equal(path, FileNameHelper.EnsureUniqueDirectoryPath(path));

        Directory.CreateDirectory(path);
        Assert.Equal($"{path} (1)", FileNameHelper.EnsureUniqueDirectoryPath(path));
    }
}
