using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ManagerDataDeletionDialogTextBuilderTests
{
    [Fact]
    public void Constants_MatchExistingDialogText()
    {
        Assert.Equal(
            "No Simple VS Manager files were found.",
            ManagerDataDeletionDialogTextBuilder.NoManagerFilesFoundMessage);
        Assert.Equal(
            "Finished moving Simple VS Manager files to the Recycle Bin.",
            ManagerDataDeletionDialogTextBuilder.FinishedMovingManagerFilesMessage);
    }

    [Fact]
    public void BuildDeletionResultMessage_FormatsDeletedPaths()
    {
        var nl = Environment.NewLine;

        Assert.Equal(
            $"Moved the following locations to the Recycle Bin:{nl}• C:\\SVSM\\Cache{nl}",
            ManagerDataDeletionDialogTextBuilder.BuildDeletionResultMessage(
                new[] { @"C:\SVSM\Cache" },
                Array.Empty<string>()));
    }

    [Fact]
    public void BuildDeletionResultMessage_FormatsNoDeletedOrFailedPaths()
    {
        Assert.Equal(
            "Finished moving Simple VS Manager files to the Recycle Bin.",
            ManagerDataDeletionDialogTextBuilder.BuildDeletionResultMessage(
                Array.Empty<string>(),
                Array.Empty<string>()));
    }

    [Fact]
    public void BuildDeletionResultMessage_FormatsDeletedAndFailedPaths()
    {
        var nl = Environment.NewLine;

        Assert.Equal(
            $"Moved the following locations to the Recycle Bin:{nl}• C:\\SVSM\\Cache{nl}{nl}" +
            $"The following locations could not be moved to the Recycle Bin. Please remove them manually:{nl}" +
            $"• C:\\SVSM\\Locked{nl}",
            ManagerDataDeletionDialogTextBuilder.BuildDeletionResultMessage(
                new[] { @"C:\SVSM\Cache" },
                new[] { @"C:\SVSM\Locked" }));
    }
}
