using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModlistBackupDialogTextBuilderTests
{
    [Fact]
    public void SelectedBackupMissingMessage_MatchesExistingCopy()
    {
        Assert.Equal(
            "The selected backup could not be found.",
            ModlistBackupDialogTextBuilder.SelectedBackupMissingMessage);
    }

    [Theory]
    [InlineData(null, "Failed to restore the backup:\nThe selected backup is not valid.")]
    [InlineData("", "Failed to restore the backup:\nThe selected backup is not valid.")]
    [InlineData("Invalid JSON.", "Failed to restore the backup:\nInvalid JSON.")]
    public void BuildRestoreFailureMessage_UsesLoaderMessageOrFallback(
        string? errorMessage,
        string expected)
    {
        Assert.Equal(
            expected,
            ModlistBackupDialogTextBuilder.BuildRestoreFailureMessage(errorMessage));
    }

    [Fact]
    public void BuildRestoredStatusMessage_FormatsBackupName()
    {
        Assert.Equal(
            "Restored backup \"Backup_Auto\".",
            ModlistBackupDialogTextBuilder.BuildRestoredStatusMessage("Backup_Auto"));
    }
}
