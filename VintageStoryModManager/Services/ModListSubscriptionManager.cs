using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

/// <summary>
///     Owns the per-mod PropertyChanged subscription lifecycle for the installed-mods and
///     search-results collections: tracks which view-models are subscribed, hooks/unhooks the
///     owner-supplied handlers as items enter and leave the collections, and re-syncs on Reset.
///     Routing of the property changes themselves stays with the owner.
/// </summary>
public sealed class ModListSubscriptionManager : IDisposable
{
    private readonly ObservableCollection<ModListItemViewModel> _installedMods;
    private readonly ObservableCollection<ModListItemViewModel> _searchResults;
    private readonly PropertyChangedEventHandler _installedModPropertyChanged;
    private readonly PropertyChangedEventHandler _searchResultPropertyChanged;
    private readonly Action<ModListItemViewModel> _onInstalledModAttached;
    private readonly Action<ModListItemViewModel> _onSearchResultAttached;
    private readonly Action _onInstalledModsChanged;

    private readonly HashSet<ModListItemViewModel> _installedModSubscriptions = new();
    private readonly HashSet<ModListItemViewModel> _searchResultSubscriptions = new();

    public ModListSubscriptionManager(
        ObservableCollection<ModListItemViewModel> installedMods,
        ObservableCollection<ModListItemViewModel> searchResults,
        PropertyChangedEventHandler installedModPropertyChanged,
        PropertyChangedEventHandler searchResultPropertyChanged,
        Action<ModListItemViewModel> onInstalledModAttached,
        Action<ModListItemViewModel> onSearchResultAttached,
        Action onInstalledModsChanged)
    {
        ArgumentNullException.ThrowIfNull(installedMods);
        ArgumentNullException.ThrowIfNull(searchResults);
        ArgumentNullException.ThrowIfNull(installedModPropertyChanged);
        ArgumentNullException.ThrowIfNull(searchResultPropertyChanged);
        ArgumentNullException.ThrowIfNull(onInstalledModAttached);
        ArgumentNullException.ThrowIfNull(onSearchResultAttached);
        ArgumentNullException.ThrowIfNull(onInstalledModsChanged);

        _installedMods = installedMods;
        _searchResults = searchResults;
        _installedModPropertyChanged = installedModPropertyChanged;
        _searchResultPropertyChanged = searchResultPropertyChanged;
        _onInstalledModAttached = onInstalledModAttached;
        _onSearchResultAttached = onSearchResultAttached;
        _onInstalledModsChanged = onInstalledModsChanged;

        _installedMods.CollectionChanged += OnModsCollectionChanged;
        _searchResults.CollectionChanged += OnSearchResultsCollectionChanged;
    }

    public IReadOnlyCollection<ModListItemViewModel> InstalledSubscriptions => _installedModSubscriptions;
    public IReadOnlyCollection<ModListItemViewModel> SearchResultSubscriptions => _searchResultSubscriptions;

    private void OnModsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var mod in EnumerateModItems(e.NewItems)) AttachInstalledMod(mod);
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var mod in EnumerateModItems(e.OldItems)) DetachInstalledMod(mod);
                break;
            case NotifyCollectionChangedAction.Replace:
                foreach (var mod in EnumerateModItems(e.OldItems)) DetachInstalledMod(mod);

                foreach (var mod in EnumerateModItems(e.NewItems)) AttachInstalledMod(mod);
                break;
            case NotifyCollectionChangedAction.Reset:
                DetachAllInstalledMods();
                foreach (var mod in _installedMods) AttachInstalledMod(mod);
                break;
        }

        _onInstalledModsChanged();
    }

    private void OnSearchResultsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var mod in EnumerateModItems(e.NewItems)) AttachSearchResult(mod);
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var mod in EnumerateModItems(e.OldItems)) DetachSearchResult(mod);
                break;
            case NotifyCollectionChangedAction.Replace:
                foreach (var mod in EnumerateModItems(e.OldItems)) DetachSearchResult(mod);

                foreach (var mod in EnumerateModItems(e.NewItems)) AttachSearchResult(mod);
                break;
            case NotifyCollectionChangedAction.Reset:
                DetachAllSearchResults();
                foreach (var mod in _searchResults) AttachSearchResult(mod);
                break;
        }

    }

    private static IEnumerable<ModListItemViewModel> EnumerateModItems(IList? items)
    {
        if (items is null) yield break;

        foreach (var item in items)
            if (item is ModListItemViewModel mod)
                yield return mod;
    }

    private void AttachInstalledMod(ModListItemViewModel mod)
    {
        if (_installedModSubscriptions.Add(mod)) mod.PropertyChanged += _installedModPropertyChanged;

        _onInstalledModAttached(mod);
    }

    private void DetachInstalledMod(ModListItemViewModel mod)
    {
        if (_installedModSubscriptions.Remove(mod)) mod.PropertyChanged -= _installedModPropertyChanged;
    }

    private void DetachAllInstalledMods()
    {
        if (_installedModSubscriptions.Count == 0) return;

        foreach (var mod in _installedModSubscriptions) mod.PropertyChanged -= _installedModPropertyChanged;

        _installedModSubscriptions.Clear();
    }

    private void AttachSearchResult(ModListItemViewModel mod)
    {
        if (_searchResultSubscriptions.Add(mod)) mod.PropertyChanged += _searchResultPropertyChanged;

        _onSearchResultAttached(mod);
    }

    private void DetachSearchResult(ModListItemViewModel mod)
    {
        if (_searchResultSubscriptions.Remove(mod)) mod.PropertyChanged -= _searchResultPropertyChanged;
    }

    private void DetachAllSearchResults()
    {
        if (_searchResultSubscriptions.Count == 0) return;

        foreach (var mod in _searchResultSubscriptions) mod.PropertyChanged -= _searchResultPropertyChanged;

        _searchResultSubscriptions.Clear();
    }

    public void Dispose()
    {
        _installedMods.CollectionChanged -= OnModsCollectionChanged;
        _searchResults.CollectionChanged -= OnSearchResultsCollectionChanged;

        DetachAllInstalledMods();
        DetachAllSearchResults();
    }
}
