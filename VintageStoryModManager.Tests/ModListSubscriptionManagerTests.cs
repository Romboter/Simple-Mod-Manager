using System.Collections.ObjectModel;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class ModListSubscriptionManagerTests
{
    private sealed class Harness
    {
        public readonly ObservableCollection<ModListItemViewModel> InstalledMods = new();
        public readonly ObservableCollection<ModListItemViewModel> SearchResults = new();
        public readonly List<ModListItemViewModel> InstalledPropertyChanges = new();
        public readonly List<ModListItemViewModel> SearchResultPropertyChanges = new();
        public readonly List<ModListItemViewModel> InstalledAttached = new();
        public readonly List<ModListItemViewModel> SearchResultAttached = new();
        public int InstalledModsChangedCount;
        public readonly ModListSubscriptionManager Manager;

        public Harness()
        {
            Manager = new ModListSubscriptionManager(
                InstalledMods,
                SearchResults,
                (sender, _) => InstalledPropertyChanges.Add((ModListItemViewModel)sender!),
                (sender, _) => SearchResultPropertyChanges.Add((ModListItemViewModel)sender!),
                mod => InstalledAttached.Add(mod),
                mod => SearchResultAttached.Add(mod),
                () => InstalledModsChangedCount++);
        }
    }

    [Fact]
    public void Add_AttachesHandler_AndFiresAttachCallback()
    {
        var harness = new Harness();
        var mod = TestData.CreateMod();

        harness.InstalledMods.Add(mod);

        Assert.Contains(mod, harness.Manager.InstalledSubscriptions);
        Assert.Contains(mod, harness.InstalledAttached);

        mod.IsSelected = true;

        Assert.Single(harness.InstalledPropertyChanges);
        Assert.Same(mod, harness.InstalledPropertyChanges[0]);
    }

    [Fact]
    public void Remove_DetachesHandler()
    {
        var harness = new Harness();
        var mod = TestData.CreateMod();
        harness.InstalledMods.Add(mod);

        harness.InstalledMods.Remove(mod);

        Assert.DoesNotContain(mod, harness.Manager.InstalledSubscriptions);

        mod.IsSelected = true;

        Assert.Empty(harness.InstalledPropertyChanges);
    }

    [Fact]
    public void Reset_Resyncs()
    {
        var harness = new Harness();
        var modA = TestData.CreateMod("moda", @"C:\data\Mods\moda.zip");
        var modB = TestData.CreateMod("modb", @"C:\data\Mods\modb.zip");
        harness.InstalledMods.Add(modA);
        harness.InstalledMods.Add(modB);

        harness.InstalledMods.Clear();
        harness.InstalledMods.Add(modA);

        Assert.Single(harness.Manager.InstalledSubscriptions);
        Assert.Contains(modA, harness.Manager.InstalledSubscriptions);

        modA.IsSelected = true;

        Assert.Single(harness.InstalledPropertyChanges);
    }

    [Fact]
    public void InstalledChange_FiresChangedCallback_SearchResultsDoNot()
    {
        var harness = new Harness();
        var mod = TestData.CreateMod();

        harness.InstalledMods.Add(mod);

        Assert.Equal(1, harness.InstalledModsChangedCount);

        var searchMod = TestData.CreateMod("search", @"C:\data\Mods\search.zip");
        harness.SearchResults.Add(searchMod);

        Assert.Equal(1, harness.InstalledModsChangedCount);
    }

    [Fact]
    public void SearchResultAttach_FiresItsCallback()
    {
        var harness = new Harness();
        var mod = TestData.CreateMod();

        harness.SearchResults.Add(mod);

        Assert.Contains(mod, harness.SearchResultAttached);
        Assert.DoesNotContain(mod, harness.InstalledAttached);
    }

    [Fact]
    public void Dispose_DetachesAll_AndStopsTracking()
    {
        var harness = new Harness();
        var installedMod = TestData.CreateMod();
        var searchMod = TestData.CreateMod("search", @"C:\data\Mods\search.zip");
        harness.InstalledMods.Add(installedMod);
        harness.SearchResults.Add(searchMod);

        harness.Manager.Dispose();

        Assert.Empty(harness.Manager.InstalledSubscriptions);
        Assert.Empty(harness.Manager.SearchResultSubscriptions);

        harness.InstalledMods.Add(TestData.CreateMod("another", @"C:\data\Mods\another.zip"));
        harness.SearchResults.Add(TestData.CreateMod("another2", @"C:\data\Mods\another2.zip"));

        Assert.Empty(harness.Manager.InstalledSubscriptions);
        Assert.Empty(harness.Manager.SearchResultSubscriptions);

        installedMod.IsSelected = true;
        searchMod.IsSelected = true;

        Assert.Empty(harness.InstalledPropertyChanges);
        Assert.Empty(harness.SearchResultPropertyChanges);
    }
}
