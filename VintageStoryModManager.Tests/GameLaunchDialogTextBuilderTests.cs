using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class GameLaunchDialogTextBuilderTests
{
    [Fact]
    public void BuildBackupFailedPromptMessage_FormatsError()
    {
        Assert.Equal(
            "The automatic VintagestoryData backup failed:\naccess denied\n\nLaunch Vintage Story without creating a backup?",
            GameLaunchDialogTextBuilder.BuildBackupFailedPromptMessage("access denied"));
    }

    [Fact]
    public void BuildBackupFailedMessage_FormatsError()
    {
        Assert.Equal(
            "The automatic VintagestoryData backup failed:\ndisk full",
            GameLaunchDialogTextBuilder.BuildBackupFailedMessage("disk full"));
    }

    [Fact]
    public void BuildShortcutLaunchFailedMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to launch Vintage Story using the shortcut:\nfile not found",
            GameLaunchDialogTextBuilder.BuildShortcutLaunchFailedMessage("file not found"));
    }

    [Fact]
    public void BuildLaunchFailedMessage_FormatsError()
    {
        Assert.Equal(
            "Failed to launch Vintage Story:\nprocess error",
            GameLaunchDialogTextBuilder.BuildLaunchFailedMessage("process error"));
    }

    [Fact]
    public void BuildInvalidShortcutMessage_FormatsError()
    {
        Assert.Equal(
            "The selected shortcut is not valid:\nbad path",
            GameLaunchDialogTextBuilder.BuildInvalidShortcutMessage("bad path"));
    }
}
