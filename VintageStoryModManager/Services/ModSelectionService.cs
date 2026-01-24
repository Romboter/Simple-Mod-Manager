using System;
using System.Collections.Generic;
using System.Linq;

namespace VintageStoryModManager.Services;

/// <summary>
/// Service responsible for calculating mod selection based on user input (Ctrl/Shift/range selection).
/// Provides testable selection algorithms without UI dependencies.
/// </summary>
public sealed class ModSelectionService
{
    /// <summary>
    /// Represents the result of a selection calculation.
    /// </summary>
    public sealed record SelectionResult(
        IReadOnlyList<int> SelectedIndices,
        int? NewAnchorIndex);

    /// <summary>
    /// Calculates the new selection based on user interaction.
    /// </summary>
    /// <param name="clickedIndex">The index of the clicked item</param>
    /// <param name="currentSelection">Current selected indices</param>
    /// <param name="anchorIndex">The current anchor index (for range selection)</param>
    /// <param name="isShiftPressed">Whether Shift key is pressed</param>
    /// <param name="isCtrlPressed">Whether Ctrl key is pressed</param>
    /// <param name="viewSize">Total number of items in the view</param>
    /// <returns>The new selection result</returns>
    public SelectionResult CalculateNewSelection(
        int clickedIndex,
        IReadOnlyList<int> currentSelection,
        int? anchorIndex,
        bool isShiftPressed,
        bool isCtrlPressed,
        int viewSize)
    {
        if (clickedIndex < 0 || clickedIndex >= viewSize)
            throw new ArgumentOutOfRangeException(nameof(clickedIndex));

        // Shift+Click: Range selection
        if (isShiftPressed)
        {
            if (anchorIndex is null or < 0)
            {
                // No anchor, start fresh
                var selection = isCtrlPressed
                    ? currentSelection.Concat(new[] { clickedIndex }).Distinct().ToList()
                    : new List<int> { clickedIndex };

                return new SelectionResult(selection, clickedIndex);
            }

            var rangeSelection = CalculateRangeSelection(
                anchorIndex.Value,
                clickedIndex,
                currentSelection,
                isCtrlPressed);

            return new SelectionResult(rangeSelection, anchorIndex.Value);
        }

        // Ctrl+Click: Toggle selection
        if (isCtrlPressed)
        {
            var toggleSelection = CalculateToggleSelection(clickedIndex, currentSelection);
            return new SelectionResult(toggleSelection, clickedIndex);
        }

        // Normal click: Single selection
        return new SelectionResult(new List<int> { clickedIndex }, clickedIndex);
    }

    /// <summary>
    /// Calculates range selection from anchor to target.
    /// </summary>
    /// <param name="anchorIndex">The anchor index</param>
    /// <param name="targetIndex">The target index</param>
    /// <param name="currentSelection">Current selected indices</param>
    /// <param name="preserveExisting">Whether to preserve existing selection</param>
    /// <returns>The new selection</returns>
    public IReadOnlyList<int> CalculateRangeSelection(
        int anchorIndex,
        int targetIndex,
        IReadOnlyList<int> currentSelection,
        bool preserveExisting)
    {
        var start = Math.Min(anchorIndex, targetIndex);
        var end = Math.Max(anchorIndex, targetIndex);

        var rangeIndices = Enumerable.Range(start, end - start + 1).ToList();

        if (preserveExisting)
        {
            // Combine with existing selection
            return currentSelection
                .Concat(rangeIndices)
                .Distinct()
                .OrderBy(i => i)
                .ToList();
        }

        return rangeIndices;
    }

    /// <summary>
    /// Calculates toggle selection (add if not selected, remove if selected).
    /// </summary>
    /// <param name="index">The index to toggle</param>
    /// <param name="currentSelection">Current selected indices</param>
    /// <returns>The new selection</returns>
    public IReadOnlyList<int> CalculateToggleSelection(
        int index,
        IReadOnlyList<int> currentSelection)
    {
        var selection = currentSelection.ToList();

        if (selection.Contains(index))
        {
            selection.Remove(index);
        }
        else
        {
            selection.Add(index);
            selection.Sort();
        }

        return selection;
    }

    /// <summary>
    /// Calculates selection for "Select All" operation.
    /// </summary>
    /// <param name="viewSize">Total number of items in the view</param>
    /// <returns>All indices in the view</returns>
    public IReadOnlyList<int> CalculateSelectAll(int viewSize)
    {
        if (viewSize <= 0) return Array.Empty<int>();

        return Enumerable.Range(0, viewSize).ToList();
    }

    /// <summary>
    /// Resolves selected items from indices.
    /// </summary>
    /// <typeparam name="T">The item type</typeparam>
    /// <param name="items">The list of all items</param>
    /// <param name="selectedIndices">The selected indices</param>
    /// <returns>The selected items</returns>
    public IReadOnlyList<T> ResolveSelection<T>(
        IReadOnlyList<T> items,
        IReadOnlyList<int> selectedIndices)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        if (selectedIndices is null) throw new ArgumentNullException(nameof(selectedIndices));

        return selectedIndices
            .Where(i => i >= 0 && i < items.Count)
            .Select(i => items[i])
            .ToList();
    }

    /// <summary>
    /// Finds the indices of items in a list.
    /// </summary>
    /// <typeparam name="T">The item type</typeparam>
    /// <param name="items">The list of all items</param>
    /// <param name="selectedItems">The items to find</param>
    /// <returns>The indices of the items</returns>
    public IReadOnlyList<int> FindIndices<T>(
        IReadOnlyList<T> items,
        IReadOnlyList<T> selectedItems)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        if (selectedItems is null) throw new ArgumentNullException(nameof(selectedItems));

        var selectedSet = new HashSet<T>(selectedItems);
        var indices = new List<int>();

        for (var i = 0; i < items.Count; i++)
        {
            if (selectedSet.Contains(items[i]))
            {
                indices.Add(i);
            }
        }

        return indices;
    }
}
