using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VintageStoryModManager.ViewModels;

/// <summary>Which of the three main tabs is active.</summary>
public enum ViewSection
{
    MainTab,
    DatabaseTab,
    ModlistTab
}

/// <summary>
///     Owns the active-tab state machine: which section is current, the internet-access gates on the
///     Database/Modlist tabs, and the three tab-switch commands. UI side effects of a switch (search/
///     selection clearing, status text, property-change notification, fast-check) stay with the owner
///     via the onSectionChanged callback, because XAML binds the owner's properties.
/// </summary>
public sealed class TabNavigationViewModel
{
    internal const string InternetAccessDisabledStatusMessage =
        "Enable Internet Access in the File menu to use.";

    private readonly Func<bool> _isInternetAccessDisabled;
    private readonly Action<string> _setStatus;              // gate-refusal status text
    private readonly Action<ViewSection> _onSectionChanged;  // fires AFTER Current changes, successful switches only
    private readonly ICollectionView _modsView;
    private readonly ICollectionView _searchResultsView;
    private readonly ICollectionView _cloudModlistsView;
    private readonly RelayCommand _showMainTabCommand;
    private readonly RelayCommand _showDatabaseTabCommand;
    private readonly RelayCommand _showModlistTabCommand;

    public TabNavigationViewModel(
        ICollectionView modsView,
        ICollectionView searchResultsView,
        ICollectionView cloudModlistsView,
        Func<bool> isInternetAccessDisabled,
        Action<string> setStatus,
        Action<ViewSection> onSectionChanged)
    {
        ArgumentNullException.ThrowIfNull(modsView);
        ArgumentNullException.ThrowIfNull(searchResultsView);
        ArgumentNullException.ThrowIfNull(cloudModlistsView);
        ArgumentNullException.ThrowIfNull(isInternetAccessDisabled);
        ArgumentNullException.ThrowIfNull(setStatus);
        ArgumentNullException.ThrowIfNull(onSectionChanged);

        _modsView = modsView;
        _searchResultsView = searchResultsView;
        _cloudModlistsView = cloudModlistsView;
        _isInternetAccessDisabled = isInternetAccessDisabled;
        _setStatus = setStatus;
        _onSectionChanged = onSectionChanged;

        _showMainTabCommand = new RelayCommand(() => SetViewSection(ViewSection.MainTab));
        _showDatabaseTabCommand = new RelayCommand(
            () => SetViewSection(ViewSection.DatabaseTab),
            () => !_isInternetAccessDisabled());
        _showModlistTabCommand = new RelayCommand(
            () => SetViewSection(ViewSection.ModlistTab),
            () => !_isInternetAccessDisabled());
    }

    public ViewSection Current { get; private set; } = ViewSection.MainTab;

    public IRelayCommand ShowMainTabCommand => _showMainTabCommand;
    public IRelayCommand ShowDatabaseTabCommand => _showDatabaseTabCommand;
    public IRelayCommand ShowModlistTabCommand => _showModlistTabCommand;

    public bool IsViewingMainTab => Current == ViewSection.MainTab;
    public bool IsViewingModlistTab => Current == ViewSection.ModlistTab;
    public bool SearchModDatabase => Current == ViewSection.DatabaseTab;

    public ICollectionView CurrentModsView => Current switch
    {
        ViewSection.DatabaseTab => _searchResultsView,
        ViewSection.ModlistTab => _cloudModlistsView,
        _ => _modsView
    };

    public void SetViewSection(ViewSection section)
    {
        if (Current == section) return;

        if (section == ViewSection.DatabaseTab && _isInternetAccessDisabled())
        {
            _setStatus(InternetAccessDisabledStatusMessage);
            return;
        }

        if (section == ViewSection.ModlistTab && _isInternetAccessDisabled())
        {
            _setStatus(InternetAccessDisabledStatusMessage);
            return;
        }

        Current = section;

        _onSectionChanged(section);
    }

    public void NotifyInternetAccessChanged()
    {
        _showDatabaseTabCommand.NotifyCanExecuteChanged();
        _showModlistTabCommand.NotifyCanExecuteChanged();
    }
}
