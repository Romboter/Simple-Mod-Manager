using VintageStoryModManager.Services;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class PresetConfigurationImportPromptTests
{
    private static PresetConfigurationImportEntry Entry(string modId, string fileName = "config.json") =>
        new(modId, fileName, null, "{}");

    [Fact]
    public void BuildImportPrompt_ResolverReturnsNull_KeepsModIdAsDisplayName()
    {
        var prompt = PresetConfigurationImportService.BuildImportPrompt(
            new[] { Entry("mymod") }, _ => null);

        Assert.Equal("mymod", prompt.ModDisplayNames["mymod"]);
        Assert.Contains("• mymod", prompt.Message);
    }

    [Fact]
    public void BuildImportPrompt_ResolverReturnsValue_UsesResolvedDisplayName()
    {
        var prompt = PresetConfigurationImportService.BuildImportPrompt(
            new[] { Entry("mymod") }, _ => "My Mod");

        Assert.Equal("My Mod", prompt.ModDisplayNames["mymod"]);
        Assert.Contains("• My Mod", prompt.Message);
    }

    [Fact]
    public void BuildImportPrompt_DictionaryUsesFirstOccurrencePerModId()
    {
        var callCount = 0;
        var prompt = PresetConfigurationImportService.BuildImportPrompt(
            new[] { Entry("mymod", "a.json"), Entry("mymod", "b.json") },
            _ =>
            {
                callCount++;
                return callCount == 1 ? "First Name" : "Second Name";
            });

        Assert.Equal("First Name", prompt.ModDisplayNames["mymod"]);
    }

    [Fact]
    public void BuildImportPrompt_PromptNames_DedupedCaseInsensitivelyAndSorted()
    {
        var prompt = PresetConfigurationImportService.BuildImportPrompt(
            new[] { Entry("modb"), Entry("moda"), Entry("modA") },
            modId => modId switch
            {
                "modb" => "Zebra",
                "moda" => "alpha",
                "modA" => "Alpha",
                _ => null
            });

        // "alpha" and "Alpha" dedupe case-insensitively (first one wins: "alpha"), then sorted before "Zebra".
        var bulletLines = prompt.Message
            .Split('\n')
            .Where(line => line.StartsWith("• "))
            .ToList();

        Assert.Equal(new[] { "• alpha", "• Zebra" }, bulletLines);
    }

    [Fact]
    public void BuildImportPrompt_Message_ContainsBulletListAndFixedTextFragments()
    {
        var prompt = PresetConfigurationImportService.BuildImportPrompt(
            new[] { Entry("mymod") }, _ => "My Mod");

        Assert.Contains(
            "This modlist includes configuration files for the following mods:",
            prompt.Message);
        Assert.Contains("• My Mod", prompt.Message);
        Assert.Contains(
            "Importing these configurations will overwrite your existing " +
            "settings for these mods if they are already installed. " +
            "Do you want to import them?",
            prompt.Message);
    }
}
