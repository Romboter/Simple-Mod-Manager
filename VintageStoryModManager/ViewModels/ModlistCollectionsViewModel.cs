using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.ViewModels;

/// <summary>
///     Owns the cloud and local modlist collections exposed to the UI. Populated externally by
///     <c>MainWindow</c>'s cloud/local-modlist workflows; this view model is purely a data holder with
///     zero coupling to the rest of <see cref="MainViewModel" />.
/// </summary>
public sealed class ModlistCollectionsViewModel : ObservableObject
{
    private readonly ObservableCollection<CloudModlistListEntry> _cloudModlists = new();
    private readonly ObservableCollection<LocalModlistListEntry> _localModlists = new();

    public ModlistCollectionsViewModel()
    {
        CloudModlistsView = CollectionViewSource.GetDefaultView(_cloudModlists);
        LocalModlistsView = CollectionViewSource.GetDefaultView(_localModlists);
    }

    public ICollectionView CloudModlistsView { get; }

    public ICollectionView LocalModlistsView { get; }

    public bool HasCloudModlists => _cloudModlists.Count > 0;

    public bool HasLocalModlists => _localModlists.Count > 0;

    public void ReplaceCloudModlists(IEnumerable<CloudModlistListEntry>? entries)
    {
        _cloudModlists.Clear();

        if (entries is not null)
            foreach (var entry in entries)
                if (entry is not null)
                    _cloudModlists.Add(entry);

        CloudModlistsView.Refresh();
        OnPropertyChanged(nameof(HasCloudModlists));
    }

    public bool TryReplaceCloudModlist(CloudModlistListEntry existing, CloudModlistListEntry replacement)
    {
        var index = _cloudModlists.IndexOf(existing);
        if (index < 0) return false;

        _cloudModlists[index] = replacement;
        CloudModlistsView.Refresh();
        OnPropertyChanged(nameof(HasCloudModlists));
        return true;
    }

    public void ReplaceLocalModlists(IEnumerable<LocalModlistListEntry>? entries)
    {
        _localModlists.Clear();

        if (entries is not null)
            foreach (var entry in entries)
                if (entry is not null)
                    _localModlists.Add(entry);

        LocalModlistsView.Refresh();
        OnPropertyChanged(nameof(HasLocalModlists));
    }
}
