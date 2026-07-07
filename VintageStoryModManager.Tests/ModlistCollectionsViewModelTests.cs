using VintageStoryModManager.Models;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModlistCollectionsViewModelTests
{
    private static CloudModlistListEntry CreateCloudEntry(string slotKey, string? name = null)
    {
        return new CloudModlistListEntry(
            ownerId: "owner-1",
            slotKey: slotKey,
            slotLabel: $"Slot {slotKey}",
            name: name ?? $"Modlist {slotKey}",
            description: null,
            version: null,
            uploader: null,
            mods: new List<string>(),
            contentJson: "{}",
            dateAdded: null,
            gameVersion: null,
            isContentComplete: true);
    }

    private static LocalModlistListEntry CreateLocalEntry(string fileName, string? name = null)
    {
        return new LocalModlistListEntry(
            filePath: $"C:/modlists/{fileName}",
            name: name ?? fileName,
            description: null,
            version: null,
            uploader: null,
            mods: new List<string>(),
            lastModified: null,
            gameVersion: null);
    }

    [Fact]
    public void ReplaceCloudModlists_UpdatesHasCloudModlists()
    {
        var vm = new ModlistCollectionsViewModel();
        Assert.False(vm.HasCloudModlists);

        vm.ReplaceCloudModlists(new[] { CreateCloudEntry("a") });

        Assert.True(vm.HasCloudModlists);
        Assert.Single(vm.CloudModlistsView.Cast<CloudModlistListEntry>());
    }

    [Fact]
    public void ReplaceCloudModlists_WithEmpty_ClearsHasCloudModlists()
    {
        var vm = new ModlistCollectionsViewModel();
        vm.ReplaceCloudModlists(new[] { CreateCloudEntry("a") });

        vm.ReplaceCloudModlists(null);

        Assert.False(vm.HasCloudModlists);
    }

    [Fact]
    public void ReplaceLocalModlists_UpdatesHasLocalModlists()
    {
        var vm = new ModlistCollectionsViewModel();
        Assert.False(vm.HasLocalModlists);

        vm.ReplaceLocalModlists(new[] { CreateLocalEntry("one.json") });

        Assert.True(vm.HasLocalModlists);
        Assert.Single(vm.LocalModlistsView.Cast<LocalModlistListEntry>());
    }

    [Fact]
    public void TryReplaceCloudModlist_SwapsTheRightEntry()
    {
        var vm = new ModlistCollectionsViewModel();
        var first = CreateCloudEntry("a");
        var second = CreateCloudEntry("b");
        vm.ReplaceCloudModlists(new[] { first, second });

        var replacement = CreateCloudEntry("b", "Renamed");
        var result = vm.TryReplaceCloudModlist(second, replacement);

        Assert.True(result);
        var entries = new List<CloudModlistListEntry>();
        foreach (var entry in vm.CloudModlistsView) entries.Add((CloudModlistListEntry)entry);

        Assert.Equal(2, entries.Count);
        Assert.Same(first, entries[0]);
        Assert.Same(replacement, entries[1]);
    }

    [Fact]
    public void TryReplaceCloudModlist_ReturnsFalse_WhenEntryNotFound()
    {
        var vm = new ModlistCollectionsViewModel();
        vm.ReplaceCloudModlists(new[] { CreateCloudEntry("a") });

        var missing = CreateCloudEntry("missing");
        var result = vm.TryReplaceCloudModlist(missing, CreateCloudEntry("replacement"));

        Assert.False(result);
    }

    [Fact]
    public void ReplaceCloudModlists_RaisesPropertyChanged_ForHasCloudModlists()
    {
        var vm = new ModlistCollectionsViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.ReplaceCloudModlists(new[] { CreateCloudEntry("a") });

        Assert.Contains(nameof(ModlistCollectionsViewModel.HasCloudModlists), raised);
    }

    [Fact]
    public void ReplaceLocalModlists_RaisesPropertyChanged_ForHasLocalModlists()
    {
        var vm = new ModlistCollectionsViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.ReplaceLocalModlists(new[] { CreateLocalEntry("one.json") });

        Assert.Contains(nameof(ModlistCollectionsViewModel.HasLocalModlists), raised);
    }

    [Fact]
    public void TryReplaceCloudModlist_RaisesPropertyChanged_ForHasCloudModlists()
    {
        var vm = new ModlistCollectionsViewModel();
        var entry = CreateCloudEntry("a");
        vm.ReplaceCloudModlists(new[] { entry });

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.TryReplaceCloudModlist(entry, CreateCloudEntry("a", "Renamed"));

        Assert.Contains(nameof(ModlistCollectionsViewModel.HasCloudModlists), raised);
    }
}
