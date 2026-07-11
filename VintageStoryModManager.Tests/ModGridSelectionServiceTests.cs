using System.Windows.Threading;
using VintageStoryModManager.Services;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public class ModGridSelectionServiceTests
{
    private int _selectionChangedCount;
    private readonly ModGridSelectionService _service;

    public ModGridSelectionServiceTests()
    {
        _service = new ModGridSelectionService(
            Dispatcher.CurrentDispatcher,
            () => _selectionChangedCount++,
            _ => { },
            _ => { });
    }

    [Fact]
    public void AddToSelection_SelectsMarksAndNotifies_IgnoresDuplicates()
    {
        var mod = TestData.CreateMod();

        _service.AddToSelection(mod);
        Assert.Single(_service.SelectedMods, mod);
        Assert.True(mod.IsSelected);
        Assert.Equal(1, _selectionChangedCount);

        _service.AddToSelection(mod);
        Assert.Single(_service.SelectedMods);
        Assert.Equal(1, _selectionChangedCount); // duplicate add does not re-notify
    }

    [Fact]
    public void RemoveFromSelection_UnmarksAndNotifies_IgnoresNonMembers()
    {
        var mod = TestData.CreateMod();
        _service.AddToSelection(mod);
        _selectionChangedCount = 0;

        _service.RemoveFromSelection(mod);
        Assert.Empty(_service.SelectedMods);
        Assert.False(mod.IsSelected);
        Assert.Equal(1, _selectionChangedCount);

        _service.RemoveFromSelection(mod);
        Assert.Equal(1, _selectionChangedCount); // removing a non-member does not notify
    }

    [Fact]
    public void SelectAllModsInCurrentView_SelectsAllAndAnchorsLast()
    {
        var mods = new List<ModListItemViewModel>
        {
            TestData.CreateMod("a", @"C:\data\Mods\a.zip"),
            TestData.CreateMod("b", @"C:\data\Mods\b.zip"),
            TestData.CreateMod("c", @"C:\data\Mods\c.zip")
        };

        _service.SelectAllModsInCurrentView(false, mods);

        Assert.Equal(mods, _service.SelectedMods);
        Assert.All(mods, m => Assert.True(m.IsSelected));
        Assert.Same(mods[2], _service.SelectionAnchor);
    }

    [Fact]
    public void SelectAllModsInCurrentView_WhileApplyingPreset_DoesNothing()
    {
        var mods = new List<ModListItemViewModel> { TestData.CreateMod() };

        _service.SelectAllModsInCurrentView(true, mods);

        Assert.Empty(_service.SelectedMods);
        Assert.Equal(0, _selectionChangedCount);
    }

    [Fact]
    public void ClearSelection_UnmarksAll_AnchorSurvivesUnlessReset()
    {
        var mods = new List<ModListItemViewModel>
        {
            TestData.CreateMod("a", @"C:\data\Mods\a.zip"),
            TestData.CreateMod("b", @"C:\data\Mods\b.zip")
        };
        _service.SelectAllModsInCurrentView(false, mods);

        _service.ClearSelection();
        Assert.Empty(_service.SelectedMods);
        Assert.All(mods, m => Assert.False(m.IsSelected));
        Assert.Same(mods[1], _service.SelectionAnchor); // characterization: anchor kept by default

        _service.SelectAllModsInCurrentView(false, mods);
        _service.ClearSelection(true);
        Assert.Null(_service.SelectionAnchor);
    }

    [Fact]
    public void ClearModDatabaseSelections_RemovesOnlyDatabaseEntries_AndClearsDatabaseAnchor()
    {
        var local = TestData.CreateMod("local", @"C:\data\Mods\local.zip");
        var db = TestData.CreateMod("dbmod", @"C:\data\Mods\dbmod.zip", location: "Mod Database");
        _service.AddToSelection(local);
        // RestoreSelection with matching anchorSourcePath makes the database entry the anchor.
        _service.RestoreSelection(new[] { local, db }, db.SourcePath);
        Assert.Same(db, _service.SelectionAnchor);
        _selectionChangedCount = 0;

        _service.ClearModDatabaseSelections();

        Assert.Single(_service.SelectedMods, local);
        Assert.False(db.IsSelected);
        Assert.True(local.IsSelected);
        Assert.Null(_service.SelectionAnchor);
        Assert.Equal(1, _selectionChangedCount);
    }

    [Fact]
    public void RestoreSelection_ReplacesSelectionAndAnchorsBySourcePathCaseInsensitively()
    {
        var old = TestData.CreateMod("old", @"C:\data\Mods\old.zip");
        _service.AddToSelection(old);

        var a = TestData.CreateMod("a", @"C:\data\Mods\a.zip");
        var b = TestData.CreateMod("b", @"C:\data\Mods\b.zip");
        _service.RestoreSelection(new[] { a, b }, @"C:\DATA\MODS\A.ZIP");

        Assert.Equal(new[] { a, b }, _service.SelectedMods);
        Assert.False(old.IsSelected);
        Assert.Same(a, _service.SelectionAnchor);
    }

    [Fact]
    public void RestoreSelection_NoAnchorMatch_AnchorsLast_EmptyRestoreClearsAnchor()
    {
        var a = TestData.CreateMod("a", @"C:\data\Mods\a.zip");
        var b = TestData.CreateMod("b", @"C:\data\Mods\b.zip");

        _service.RestoreSelection(new[] { a, b }, @"C:\data\Mods\unknown.zip");
        Assert.Same(b, _service.SelectionAnchor);

        _service.RestoreSelection(Array.Empty<ModListItemViewModel>(), null);
        Assert.Empty(_service.SelectedMods);
        Assert.Null(_service.SelectionAnchor);
    }

    [Fact]
    public void RestoreSelection_IdenticalSelection_DoesNotNotify()
    {
        var a = TestData.CreateMod("a", @"C:\data\Mods\a.zip");
        var b = TestData.CreateMod("b", @"C:\data\Mods\b.zip");
        var mods = new[] { a, b };
        _service.RestoreSelection(mods, null);
        _selectionChangedCount = 0;

        _service.RestoreSelection(mods, null);

        Assert.Equal(0, _selectionChangedCount);
        Assert.Equal(mods, _service.SelectedMods);
    }
}
