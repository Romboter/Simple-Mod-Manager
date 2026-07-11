#nullable enable

using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Services;

internal sealed class ModGridSelectionService
{
    private readonly Dispatcher _dispatcher;
    private readonly Action _onSelectionChanged;
    private readonly Action<ModListItemViewModel> _onFixButtonRefreshNeeded;
    private readonly Action<ModListItemViewModel> _onCopyButtonRefreshNeeded;

    private readonly List<ModListItemViewModel> _selectedMods = new();
    private readonly Dictionary<ModListItemViewModel, PropertyChangedEventHandler> _selectedModPropertyHandlers = new();
    private ModListItemViewModel? _selectionAnchor;

    public ModGridSelectionService(
        Dispatcher dispatcher,
        Action onSelectionChanged,
        Action<ModListItemViewModel> onFixButtonRefreshNeeded,
        Action<ModListItemViewModel> onCopyButtonRefreshNeeded)
    {
        _dispatcher = dispatcher;
        _onSelectionChanged = onSelectionChanged;
        _onFixButtonRefreshNeeded = onFixButtonRefreshNeeded;
        _onCopyButtonRefreshNeeded = onCopyButtonRefreshNeeded;
    }

    public IReadOnlyList<ModListItemViewModel> SelectedMods => _selectedMods;

    public ModListItemViewModel? SelectionAnchor => _selectionAnchor;

    public void HandleModRowSelection(
        ModListItemViewModel mod,
        bool isApplyingPreset,
        List<ModListItemViewModel> modsInViewOrder)
    {
        if (isApplyingPreset) return;

        var isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var isCtrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        if (isShiftPressed)
        {
            if (_selectionAnchor is not { } anchor)
            {
                if (!isCtrlPressed) ClearSelection();

                AddToSelection(mod);
                _selectionAnchor = mod;
                return;
            }

            var anchorApplied = ApplyRangeSelection(anchor, mod, isCtrlPressed, modsInViewOrder);
            if (!anchorApplied) _selectionAnchor = mod;

            return;
        }

        if (isCtrlPressed)
        {
            if (_selectedMods.Contains(mod))
            {
                RemoveFromSelection(mod);
                _selectionAnchor = mod;
            }
            else
            {
                AddToSelection(mod);
                _selectionAnchor = mod;
            }

            return;
        }

        ClearSelection();
        AddToSelection(mod);
        _selectionAnchor = mod;
    }

    public void SelectAllModsInCurrentView(bool isApplyingPreset, List<ModListItemViewModel> modsInViewOrder)
    {
        if (isApplyingPreset) return;

        ClearSelection(true);

        if (modsInViewOrder.Count == 0) return;

        foreach (var mod in modsInViewOrder) AddToSelection(mod);

        _selectionAnchor = modsInViewOrder[modsInViewOrder.Count - 1];
    }

    private bool ApplyRangeSelection(
        ModListItemViewModel start,
        ModListItemViewModel end,
        bool preserveExisting,
        List<ModListItemViewModel> modsInViewOrder)
    {
        var startIndex = modsInViewOrder.IndexOf(start);
        var endIndex = modsInViewOrder.IndexOf(end);

        if (startIndex < 0 || endIndex < 0)
        {
            if (!preserveExisting) ClearSelection();

            AddToSelection(end);
            return false;
        }

        if (!preserveExisting) ClearSelection();

        if (startIndex > endIndex) (startIndex, endIndex) = (endIndex, startIndex);

        for (var i = startIndex; i <= endIndex; i++) AddToSelection(modsInViewOrder[i]);

        return true;
    }

    public void AddToSelection(ModListItemViewModel mod)
    {
        if (_selectedMods.Contains(mod)) return;

        _selectedMods.Add(mod);
        SubscribeToSelectedMod(mod);
        mod.IsSelected = true;
        _onSelectionChanged();
    }

    public void RemoveFromSelection(ModListItemViewModel mod)
    {
        if (!_selectedMods.Remove(mod)) return;

        mod.IsSelected = false;
        UnsubscribeFromSelectedMod(mod);
        _onSelectionChanged();
    }

    public void ClearSelection(bool resetAnchor = false)
    {
        if (_selectedMods.Count > 0)
        {
            foreach (var mod in _selectedMods)
            {
                mod.IsSelected = false;
                UnsubscribeFromSelectedMod(mod);
            }

            _selectedMods.Clear();
        }

        if (resetAnchor) _selectionAnchor = null;

        _onSelectionChanged();
    }

    public void ClearModDatabaseSelections()
    {
        if (_selectedMods.Count > 0)
        {
            var removedAny = false;

            for (var i = _selectedMods.Count - 1; i >= 0; i--)
            {
                var mod = _selectedMods[i];
                if (!mod.IsModDatabaseEntry) continue;

                _selectedMods.RemoveAt(i);
                mod.IsSelected = false;
                UnsubscribeFromSelectedMod(mod);
                removedAny = true;
            }

            if (removedAny)
            {
                if (_selectionAnchor is { } anchor && anchor.IsModDatabaseEntry) _selectionAnchor = null;
                _onSelectionChanged();
            }
        }
    }

    public void RestoreSelection(IReadOnlyList<ModListItemViewModel> resolvedMods, string? anchorSourcePath)
    {
        var selectionChanged = resolvedMods.Count != _selectedMods.Count;
        if (!selectionChanged)
            for (var i = 0; i < resolvedMods.Count; i++)
                if (!ReferenceEquals(resolvedMods[i], _selectedMods[i]))
                {
                    selectionChanged = true;
                    break;
                }

        if (!selectionChanged)
        {
            UpdateSelectionAnchorAfterRestore(resolvedMods, anchorSourcePath);
            return;
        }

        foreach (var mod in _selectedMods)
        {
            mod.IsSelected = false;
            UnsubscribeFromSelectedMod(mod);
        }

        _selectedMods.Clear();

        foreach (var mod in resolvedMods)
        {
            _selectedMods.Add(mod);
            mod.IsSelected = true;
            SubscribeToSelectedMod(mod);
        }

        UpdateSelectionAnchorAfterRestore(resolvedMods, anchorSourcePath);
        _onSelectionChanged();
    }

    private void UpdateSelectionAnchorAfterRestore(
        IReadOnlyList<ModListItemViewModel> selection,
        string? anchorSourcePath)
    {
        if (selection.Count == 0)
        {
            _selectionAnchor = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(anchorSourcePath))
            foreach (var mod in selection)
                if (string.Equals(mod.SourcePath, anchorSourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    _selectionAnchor = mod;
                    return;
                }

        _selectionAnchor = selection[selection.Count - 1];
    }

    private void SubscribeToSelectedMod(ModListItemViewModel mod)
    {
        if (_selectedModPropertyHandlers.ContainsKey(mod)) return;

        PropertyChangedEventHandler handler = (_, args) =>
        {
            var shouldRefreshFixButton = string.IsNullOrEmpty(args.PropertyName)
                                         || args.PropertyName == nameof(ModListItemViewModel.CanFixDependencyIssues)
                                         || args.PropertyName == nameof(ModListItemViewModel.HasDependencyIssues)
                                         || args.PropertyName == nameof(ModListItemViewModel.MissingDependencies)
                                         || args.PropertyName == nameof(ModListItemViewModel.DependencyHasErrors);

            var shouldRefreshCopyButton = string.IsNullOrEmpty(args.PropertyName)
                                          || args.PropertyName == nameof(ModListItemViewModel.Version);

            if (!shouldRefreshFixButton && !shouldRefreshCopyButton) return;

            void RefreshButtons()
            {
                if (shouldRefreshFixButton) RefreshSelectedModFixButton(mod);

                if (shouldRefreshCopyButton) RefreshSelectedModCopyForServerButton(mod);
            }

            if (_dispatcher.CheckAccess())
                RefreshButtons();
            else
                _dispatcher.Invoke(RefreshButtons);
        };

        mod.PropertyChanged += handler;
        _selectedModPropertyHandlers[mod] = handler;
    }

    private void UnsubscribeFromSelectedMod(ModListItemViewModel mod)
    {
        if (_selectedModPropertyHandlers.TryGetValue(mod, out var handler))
        {
            mod.PropertyChanged -= handler;
            _selectedModPropertyHandlers.Remove(mod);
        }
    }

    private void RefreshSelectedModFixButton(ModListItemViewModel mod)
    {
        if (_selectedMods.Count == 1 && ReferenceEquals(_selectedMods[0], mod)) _onFixButtonRefreshNeeded(mod);
    }

    private void RefreshSelectedModCopyForServerButton(ModListItemViewModel mod)
    {
        if (_selectedMods.Count == 1 && ReferenceEquals(_selectedMods[0], mod)) _onCopyButtonRefreshNeeded(mod);
    }
}
