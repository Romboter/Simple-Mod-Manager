using System.ComponentModel;
using System.Windows.Data;
using VintageStoryModManager.ViewModels;
using Xunit;

namespace VintageStoryModManager.Tests;

public sealed class TabNavigationViewModelTests
{
    private static ICollectionView CreateView() => new ListCollectionView(new List<object>());

    private sealed record Fixture(
        TabNavigationViewModel ViewModel,
        ICollectionView ModsView,
        ICollectionView SearchResultsView,
        ICollectionView CloudModlistsView,
        List<string> StatusMessages,
        List<ViewSection> SectionChanges,
        Func<bool> IsOffline);

    private static Fixture CreateFixture(bool offline = false)
    {
        var modsView = CreateView();
        var searchResultsView = CreateView();
        var cloudModlistsView = CreateView();
        var statusMessages = new List<string>();
        var sectionChanges = new List<ViewSection>();
        var isOffline = offline;

        var viewModel = new TabNavigationViewModel(
            modsView,
            searchResultsView,
            cloudModlistsView,
            () => isOffline,
            statusMessages.Add,
            sectionChanges.Add);

        return new Fixture(
            viewModel,
            modsView,
            searchResultsView,
            cloudModlistsView,
            statusMessages,
            sectionChanges,
            () => isOffline);
    }

    [Fact]
    public void SetViewSection_SameSection_NoCallbacks()
    {
        var fixture = CreateFixture();

        fixture.ViewModel.SetViewSection(ViewSection.MainTab);

        Assert.Equal(ViewSection.MainTab, fixture.ViewModel.Current);
        Assert.Empty(fixture.StatusMessages);
        Assert.Empty(fixture.SectionChanges);
    }

    [Fact]
    public void SetViewSection_DatabaseTab_Offline_Blocked()
    {
        var fixture = CreateFixture(offline: true);

        fixture.ViewModel.SetViewSection(ViewSection.DatabaseTab);

        Assert.Equal(ViewSection.MainTab, fixture.ViewModel.Current);
        Assert.Contains(TabNavigationViewModel.InternetAccessDisabledStatusMessage, fixture.StatusMessages);
        Assert.Empty(fixture.SectionChanges);
    }

    [Fact]
    public void SetViewSection_ModlistTab_Offline_Blocked()
    {
        var fixture = CreateFixture(offline: true);

        fixture.ViewModel.SetViewSection(ViewSection.ModlistTab);

        Assert.Equal(ViewSection.MainTab, fixture.ViewModel.Current);
        Assert.Contains(TabNavigationViewModel.InternetAccessDisabledStatusMessage, fixture.StatusMessages);
        Assert.Empty(fixture.SectionChanges);
    }

    [Fact]
    public void SetViewSection_Online_Switches()
    {
        var fixture = CreateFixture();

        fixture.ViewModel.SetViewSection(ViewSection.DatabaseTab);

        Assert.Equal(ViewSection.DatabaseTab, fixture.ViewModel.Current);
        Assert.Equal(new[] { ViewSection.DatabaseTab }, fixture.SectionChanges);
        Assert.Empty(fixture.StatusMessages);
    }

    [Fact]
    public void CurrentModsView_MapsPerSection()
    {
        var fixture = CreateFixture();

        Assert.Same(fixture.ModsView, fixture.ViewModel.CurrentModsView);

        fixture.ViewModel.SetViewSection(ViewSection.DatabaseTab);
        Assert.Same(fixture.SearchResultsView, fixture.ViewModel.CurrentModsView);

        fixture.ViewModel.SetViewSection(ViewSection.ModlistTab);
        Assert.Same(fixture.CloudModlistsView, fixture.ViewModel.CurrentModsView);
    }

    [Fact]
    public void ShowDatabaseTabCommand_CanExecute_TracksInternetAccess()
    {
        var modsView = CreateView();
        var searchResultsView = CreateView();
        var cloudModlistsView = CreateView();
        var offline = true;

        var viewModel = new TabNavigationViewModel(
            modsView,
            searchResultsView,
            cloudModlistsView,
            () => offline,
            _ => { },
            _ => { });

        Assert.False(viewModel.ShowDatabaseTabCommand.CanExecute(null));
        Assert.False(viewModel.ShowModlistTabCommand.CanExecute(null));
        Assert.True(viewModel.ShowMainTabCommand.CanExecute(null));

        offline = false;
        viewModel.NotifyInternetAccessChanged();

        Assert.True(viewModel.ShowDatabaseTabCommand.CanExecute(null));
        Assert.True(viewModel.ShowModlistTabCommand.CanExecute(null));
        Assert.True(viewModel.ShowMainTabCommand.CanExecute(null));
    }

    [Fact]
    public void NotifyInternetAccessChanged_RaisesCanExecuteChanged()
    {
        var fixture = CreateFixture();
        var raised = false;
        fixture.ViewModel.ShowDatabaseTabCommand.CanExecuteChanged += (_, _) => raised = true;

        fixture.ViewModel.NotifyInternetAccessChanged();

        Assert.True(raised);
    }
}
