using VintageStoryModManager.Models;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class LocalModlistDialogTextBuilderTests
{
    [Fact]
    public void BuildDeleteConfirmation_SingleEntry_NamesModlist()
    {
        var entries = new[]
        {
            CreateEntry("Example Pack")
        };

        var message = LocalModlistDialogTextBuilder.BuildDeleteConfirmation(entries);

        Assert.Equal(
            "Are you sure you want to delete the modlist \"Example Pack\"? This cannot be undone.",
            message);
    }

    [Fact]
    public void BuildDeleteConfirmation_MultipleEntries_UsesCount()
    {
        var entries = new[]
        {
            CreateEntry("First"),
            CreateEntry("Second")
        };

        var message = LocalModlistDialogTextBuilder.BuildDeleteConfirmation(entries);

        Assert.Equal(
            "Are you sure you want to delete the 2 selected modlists? This cannot be undone.",
            message);
    }

    [Fact]
    public void BuildDeletionFailureMessage_FormatsErrors()
    {
        var message = LocalModlistDialogTextBuilder.BuildDeletionFailureMessage(
            new[] { "First: denied", "Second: locked" });

        Assert.Equal(
            "Some modlists could not be deleted:\n\u2022 First: denied\n\u2022 Second: locked",
            message);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "Deleted local modlist.")]
    [InlineData(3, "Deleted 3 local modlists.")]
    public void BuildDeletionStatusMessage_FormatsCounts(int deletedCount, string? expected)
    {
        Assert.Equal(
            expected,
            LocalModlistDialogTextBuilder.BuildDeletionStatusMessage(deletedCount));
    }

    [Fact]
    public void BuildCatalogFailureMessage_FormatsErrors()
    {
        var message = LocalModlistDialogTextBuilder.BuildCatalogFailureMessage(
            new[] { "bad.json: invalid", "locked.pdf: denied" });

        Assert.Equal(
            "Some local modlists could not be loaded:\n\u2022 bad.json: invalid\n\u2022 locked.pdf: denied",
            message);
    }

    [Fact]
    public void BuildUpdateFailureMessage_FormatsStages()
    {
        Assert.Equal(
            "Failed to read the modlist:\ndenied",
            LocalModlistDialogTextBuilder.BuildUpdateFailureMessage(
                LocalModlistUpdateResult.Failed(LocalModlistUpdateFailureStage.Read, "denied")));

        Assert.Equal(
            "Failed to update the modlist:\ndenied",
            LocalModlistDialogTextBuilder.BuildUpdateFailureMessage(
                LocalModlistUpdateResult.Failed(LocalModlistUpdateFailureStage.Write, "denied")));

        Assert.Equal(
            "denied",
            LocalModlistDialogTextBuilder.BuildUpdateFailureMessage(
                LocalModlistUpdateResult.Failed(LocalModlistUpdateFailureStage.Parse, "denied")));
    }

    private static LocalModlistListEntry CreateEntry(string name)
    {
        return new LocalModlistListEntry(
            $"C:/mods/{name}.json",
            name,
            null,
            null,
            null,
            Array.Empty<string>(),
            null,
            null);
    }
}
