using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VintageStoryModManager.ViewModels;

/// <summary>
/// ViewModel for custom multi-select tracking of mod list items.
/// Extracted from MainWindow.xaml.cs Phase 4.
/// Handles Ctrl/Shift selection, range selection, and selection state management.
/// </summary>
public partial class ModSelectionViewModel : ObservableObject
{
    private readonly ObservableCollection<ModListItemViewModel> _selectedMods = new();
    private readonly Dictionary<ModListItemViewModel, PropertyChangedEventHandler> _propertyHandlers = new();

    [ObservableProperty]
    private ModListItemViewModel? _selectionAnchor;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _hasMultipleSelection;

    [ObservableProperty]
    private ModListItemViewModel? _singleSelection;

    public IReadOnlyList<ModListItemViewModel> SelectedMods => _selectedMods;

    public ModSelectionViewModel()
    {
        _selectedMods.CollectionChanged += SelectedMods_CollectionChanged;
    }

    private void SelectedMods_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        HasSelection = _selectedMods.Count > 0;
        HasMultipleSelection = _selectedMods.Count > 1;
        SingleSelection = _selectedMods.Count == 1 ? _selectedMods[0] : null;

        OnSelectionChanged?.Invoke();
    }

    /// <summary>
    /// Handles mod row selection with Ctrl/Shift modifier support.
    /// </summary>
    public void HandleModRowSelection(ModListItemViewModel mod, bool isShiftPressed, bool isCtrlPressed, bool isApplyingPreset = false)
    {
        if (isApplyingPreset) return;

        if (isShiftPressed)
        {
            if (SelectionAnchor is not { } anchor)
            {
                if (!isCtrlPressed) ClearSelection();

                AddToSelection(mod);
                SelectionAnchor = mod;
                return;
            }

            var anchorApplied = ApplyRangeSelection(anchor, mod, isCtrlPressed);
            if (!anchorApplied) SelectionAnchor = mod;

            return;
        }

        if (isCtrlPressed)
        {
            if (_selectedMods.Contains(mod))
            {
                RemoveFromSelection(mod);
                SelectionAnchor = mod;
            }
            else
            {
                AddToSelection(mod);
                SelectionAnchor = mod;
            }

            return;
        }

        ClearSelection();
        AddToSelection(mod);
        SelectionAnchor = mod;
    }

    /// <summary>
    /// Selects all mods in the current view.
    /// </summary>
    [RelayCommand]
    public void SelectAllMods(bool isApplyingPreset = false)
    {
        if (isApplyingPreset) return;

        var mods = OnGetModsInViewOrder?.Invoke() ?? Array.Empty<ModListItemViewModel>();
        ClearSelection(resetAnchor: true);

        if (mods.Count == 0) return;

        foreach (var mod in mods) AddToSelection(mod);

        SelectionAnchor = mods[mods.Count - 1];
    }

    /// <summary>
    /// Applies range selection between anchor and target mod.
    /// </summary>
    private bool ApplyRangeSelection(ModListItemViewModel start, ModListItemViewModel end, bool preserveExisting)
    {
        var mods = OnGetModsInViewOrder?.Invoke() ?? Array.Empty<ModListItemViewModel>();
        var startIndex = mods.ToList().IndexOf(start);
        var endIndex = mods.ToList().IndexOf(end);

        if (startIndex < 0 || endIndex < 0)
        {
            if (!preserveExisting) ClearSelection();

            AddToSelection(end);
            return false;
        }

        if (!preserveExisting) ClearSelection();

        if (startIndex > endIndex) (startIndex, endIndex) = (endIndex, startIndex);

        for (var i = startIndex; i <= endIndex; i++) AddToSelection(mods[i]);

        return true;
    }

    /// <summary>
    /// Adds a mod to the selection.
    /// </summary>
    public void AddToSelection(ModListItemViewModel mod)
    {
        if (_selectedMods.Contains(mod)) return;

        _selectedMods.Add(mod);
        SubscribeToMod(mod);
        mod.IsSelected = true;
    }

    /// <summary>
    /// Removes a mod from the selection.
    /// </summary>
    public void RemoveFromSelection(ModListItemViewModel mod)
    {
        if (!_selectedMods.Remove(mod)) return;

        mod.IsSelected = false;
        UnsubscribeFromMod(mod);
    }

    /// <summary>
    /// Clears all selections.
    /// </summary>
    [RelayCommand]
    public void ClearSelection(bool resetAnchor = false)
    {
        if (_selectedMods.Count > 0)
        {
            foreach (var mod in _selectedMods)
            {
                mod.IsSelected = false;
                UnsubscribeFromMod(mod);
            }

            _selectedMods.Clear();
        }

        if (resetAnchor) SelectionAnchor = null;
    }

    /// <summary>
    /// Clears selections for mod database entries (when switching tabs).
    /// </summary>
    public void ClearModDatabaseSelections()
    {
        if (_selectedMods.Count == 0) return;

        var removedAny = false;

        for (var i = _selectedMods.Count - 1; i >= 0; i--)
        {
            var mod = _selectedMods[i];
            if (!mod.IsModDatabaseEntry) continue;

            _selectedMods.RemoveAt(i);
            mod.IsSelected = false;
            UnsubscribeFromMod(mod);
            removedAny = true;
        }

        if (removedAny)
        {
            if (SelectionAnchor is { } anchor && anchor.IsModDatabaseEntry) SelectionAnchor = null;
        }
    }

    /// <summary>
    /// Restores selection after mod list refresh (by source paths).
    /// </summary>
    public void RestoreSelectionFromSourcePaths(IReadOnlyList<string> sourcePaths, string? anchorSourcePath)
    {
        var resolved = new List<ModListItemViewModel>(sourcePaths.Count);
        foreach (var path in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            var current = OnFindModBySourcePath?.Invoke(path);
            if (current != null && !resolved.Contains(current)) resolved.Add(current);
        }

        var selectionChanged = resolved.Count != _selectedMods.Count;
        if (!selectionChanged)
            for (var i = 0; i < resolved.Count; i++)
                if (!ReferenceEquals(resolved[i], _selectedMods[i]))
                {
                    selectionChanged = true;
                    break;
                }

        if (!selectionChanged)
        {
            UpdateSelectionAnchorAfterRestore(resolved, anchorSourcePath);
            return;
        }

        foreach (var mod in _selectedMods)
        {
            mod.IsSelected = false;
            UnsubscribeFromMod(mod);
        }

        _selectedMods.Clear();

        foreach (var mod in resolved)
        {
            _selectedMods.Add(mod);
            mod.IsSelected = true;
            SubscribeToMod(mod);
        }

        UpdateSelectionAnchorAfterRestore(resolved, anchorSourcePath);
    }

    private void UpdateSelectionAnchorAfterRestore(IReadOnlyList<ModListItemViewModel> selection, string? anchorSourcePath)
    {
        if (selection.Count == 0)
        {
            SelectionAnchor = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(anchorSourcePath))
            foreach (var mod in selection)
                if (string.Equals(mod.SourcePath, anchorSourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    SelectionAnchor = mod;
                    return;
                }

        SelectionAnchor = selection[selection.Count - 1];
    }

    /// <summary>
    /// Gets a snapshot of selected source paths for preservation during refresh.
    /// </summary>
    public (List<string> SourcePaths, string? AnchorPath) GetSelectionSnapshot()
    {
        var sourcePaths = new List<string>(_selectedMods.Count);
        var dedup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var selected in _selectedMods)
        {
            var sourcePath = selected.SourcePath;
            if (string.IsNullOrWhiteSpace(sourcePath)) continue;

            if (dedup.Add(sourcePath)) sourcePaths.Add(sourcePath);
        }

        string? anchorPath = null;
        if (sourcePaths.Count > 0 && SelectionAnchor is { } anchor)
            anchorPath = anchor.SourcePath;

        return (sourcePaths, anchorPath);
    }

    private void SubscribeToMod(ModListItemViewModel mod)
    {
        if (_propertyHandlers.ContainsKey(mod)) return;

        PropertyChangedEventHandler handler = (_, args) =>
        {
            var shouldNotify = string.IsNullOrEmpty(args.PropertyName)
                               || args.PropertyName == nameof(ModListItemViewModel.CanFixDependencyIssues)
                               || args.PropertyName == nameof(ModListItemViewModel.HasDependencyIssues)
                               || args.PropertyName == nameof(ModListItemViewModel.MissingDependencies)
                               || args.PropertyName == nameof(ModListItemViewModel.DependencyHasErrors)
                               || args.PropertyName == nameof(ModListItemViewModel.Version);

            if (shouldNotify) OnSelectedModPropertyChanged?.Invoke(mod, args.PropertyName);
        };

        mod.PropertyChanged += handler;
        _propertyHandlers[mod] = handler;
    }

    private void UnsubscribeFromMod(ModListItemViewModel mod)
    {
        if (_propertyHandlers.TryGetValue(mod, out var handler))
        {
            mod.PropertyChanged -= handler;
            _propertyHandlers.Remove(mod);
        }
    }

    /// <summary>
    /// Event raised when selection changes.
    /// </summary>
    public event Action? OnSelectionChanged;

    /// <summary>
    /// Callback to get mods in current view order (for range selection).
    /// </summary>
    public event Func<IReadOnlyList<ModListItemViewModel>>? OnGetModsInViewOrder;

    /// <summary>
    /// Callback to find a mod by source path (for restore after refresh).
    /// </summary>
    public event Func<string, ModListItemViewModel?>? OnFindModBySourcePath;

    /// <summary>
    /// Event raised when a selected mod's relevant property changes.
    /// </summary>
    public event Action<ModListItemViewModel, string?>? OnSelectedModPropertyChanged;
}
