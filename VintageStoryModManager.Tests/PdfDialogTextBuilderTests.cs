using System.IO;
using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class PdfDialogTextBuilderTests
{
    [Fact]
    public void Constants_MatchExistingDialogText()
    {
        Assert.Equal(
            "Mods are still loading. Please try again once loading is complete.",
            PdfDialogTextBuilder.ModsStillLoadingMessage);
        Assert.Equal(
            "No installed mods were found to include in the PDF.",
            PdfDialogTextBuilder.NoInstalledModsMessage);
        Assert.Equal(
            "Saved installed mods PDF successfully.",
            PdfDialogTextBuilder.SavedSuccessfullyMessage);
    }

    [Fact]
    public void BuildReplaceExistingMessage_UsesFileNameOnly()
    {
        var message = PdfDialogTextBuilder.BuildReplaceExistingMessage(
            Path.Combine("C:", "Modlists", "My Pack.pdf"));

        Assert.Equal(
            "A modlist PDF named \"My Pack.pdf\" already exists in the Modlists folder. Do you want to replace it?",
            message);
    }

    [Fact]
    public void BuildPrepareFolderFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to prepare the Modlists folder:\naccess denied",
            PdfDialogTextBuilder.BuildPrepareFolderFailureMessage("access denied"));
    }

    [Fact]
    public void BuildSaveFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to save the PDF:\ndisk full",
            PdfDialogTextBuilder.BuildSaveFailureMessage("disk full"));
    }

    [Fact]
    public void BuildGenerateFailureMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to generate the PDF:\nrender failed",
            PdfDialogTextBuilder.BuildGenerateFailureMessage("render failed"));
    }
}
