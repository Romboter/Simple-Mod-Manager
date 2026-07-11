using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ConfigurationMigrationDialogTextBuilderTests
{
    [Fact]
    public void MigrationPromptMessage_MatchesExistingCopy()
    {
        Assert.Equal(
            "Simple VS Manager is moving its configuration and cache files from Documents to AppData/Local for better system integration.\n\n" +
            "Old location: Documents\\Simple VS Manager\n" +
            "New location: AppData\\Local\\Simple VS Manager\n\n" +
            "Would you like to copy your existing settings and cache to the new location?\n\n" +
            "Selecting 'No' will start fresh with default settings.",
            ConfigurationMigrationDialogTextBuilder.MigrationPromptMessage);
    }

    [Fact]
    public void MigrationSuccessMessage_MatchesExistingCopy()
    {
        Assert.Equal(
            "Configuration and cache files have been successfully migrated to the new location.",
            ConfigurationMigrationDialogTextBuilder.MigrationSuccessMessage);
    }

    [Fact]
    public void MigrationFailureMessage_MatchesExistingCopy()
    {
        Assert.Equal(
            "Migration could not be completed. Starting with default settings.\n\nYou can manually copy files from Documents\\Simple VS Manager to AppData\\Local\\Simple VS Manager if needed.",
            ConfigurationMigrationDialogTextBuilder.MigrationFailureMessage);
    }
}
