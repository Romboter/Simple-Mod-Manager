using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModDeletionPromptBuilderTests
{
    [Fact]
    public void BuildConfirmationMessage_TenOrFewerNames_ListsAllWithNoEllipsis()
    {
        var names = Enumerable.Range(1, 10).Select(i => (string?)$"Mod{i}").ToList();

        var message = ModDeletionPromptBuilder.BuildConfirmationMessage(names);

        foreach (var name in names)
            Assert.Contains($"• {name}", message);

        Assert.DoesNotContain("• …", message);
    }

    [Fact]
    public void BuildConfirmationMessage_MoreThanTenNames_ListsExactlyTenPlusEllipsis()
    {
        var names = Enumerable.Range(1, 15).Select(i => (string?)$"Mod{i}").ToList();

        var message = ModDeletionPromptBuilder.BuildConfirmationMessage(names);

        for (var i = 1; i <= 10; i++)
            Assert.Contains($"• Mod{i}", message);

        for (var i = 11; i <= 15; i++)
            Assert.DoesNotContain($"• Mod{i}", message);

        Assert.Contains("• …", message);
    }

    [Fact]
    public void BuildConfirmationMessage_HeaderContainsCount()
    {
        var names = new List<string?> { "Mod1", "Mod2", "Mod3" };

        var message = ModDeletionPromptBuilder.BuildConfirmationMessage(names);

        Assert.Contains("delete 3 mods?", message);
    }
}
