#nullable enable

using System.ComponentModel;
using System.Windows.Controls;
using VintageStoryModManager.Helpers;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void UpdateSearchSortingBehavior(bool isSearchingModDatabase)
    {
        if (ModsDataGrid == null) return;

        if (!isSearchingModDatabase) ModsDataGrid.CanUserSortColumns = true;
    }

    private void RestoreSortPreference()
    {
        var viewModel = _viewModel;
        if (viewModel is null) return;

        var preference = _userConfiguration.GetModListSortPreference();
        var sortMemberPath = preference.SortMemberPath;
        if (!string.IsNullOrWhiteSpace(sortMemberPath))
            ApplyModListSort(sortMemberPath, preference.Direction, false);
        else
            UpdateSortPreferenceFromSelectedOption(false);
    }

    private void ApplyModListSort(string sortMemberPath, ListSortDirection direction, bool persistPreference)
    {
        if (_viewModel is null) return;

        if (string.IsNullOrWhiteSpace(sortMemberPath)) return;

        sortMemberPath = ModSortHelper.NormalizeSortMemberPath(sortMemberPath.Trim());

        var option = FindMatchingSortOption(sortMemberPath, direction);
        if (option is null)
        {
            var sorts = ModSortHelper.BuildSortDescriptions(sortMemberPath, direction);
            var displayName = ModSortHelper.BuildSortDisplayName(sortMemberPath, direction);
            option = new SortOption(displayName, sorts);
        }

        ApplySortOption(option, persistPreference);
    }

    private void ApplySortOption(SortOption option, bool persistPreference)
    {
        if (_viewModel is null) return;

        var changed = !ReferenceEquals(_viewModel.SelectedSortOption, option);
        var previousSuppression = _suppressSortPreferenceSave;
        _suppressSortPreferenceSave = !persistPreference;

        try
        {
            if (changed)
            {
                _viewModel.SelectedSortOption = option;
            }
            else
            {
                option.Apply(_viewModel.ModsView);
                UpdateSortPreferenceFromSelectedOption(persistPreference);
            }
        }
        finally
        {
            _suppressSortPreferenceSave = previousSuppression;
        }
    }

    private SortOption? FindMatchingSortOption(string sortMemberPath, ListSortDirection direction)
    {
        if (_viewModel is null) return null;

        sortMemberPath = ModSortHelper.NormalizeSortMemberPath(sortMemberPath);

        foreach (var option in _viewModel.SortOptions)
            if (ModSortHelper.SortOptionMatches(option, sortMemberPath, direction))
                return option;

        if (_viewModel.SelectedSortOption != null
            && ModSortHelper.SortOptionMatches(_viewModel.SelectedSortOption, sortMemberPath, direction))
            return _viewModel.SelectedSortOption;

        return null;
    }

    private void UpdateSortPreferenceFromSelectedOption(bool persistPreference)
    {
        if (_viewModel?.SelectedSortOption is not { } option)
        {
            ClearColumnSortIndicators();
            if (persistPreference) _userConfiguration.SetModListSortPreference(null, ListSortDirection.Ascending);

            return;
        }

        if (option.SortDescriptions.Count == 0)
        {
            ClearColumnSortIndicators();
            if (persistPreference) _userConfiguration.SetModListSortPreference(null, ListSortDirection.Ascending);

            return;
        }

        var primary = option.SortDescriptions[0];
        UpdateColumnSortVisuals(primary.Property, primary.Direction);

        if (persistPreference) _userConfiguration.SetModListSortPreference(primary.Property, primary.Direction);
    }

    private void UpdateColumnSortVisuals(string sortMemberPath, ListSortDirection direction)
    {
        if (ModsDataGrid == null) return;

        foreach (var column in ModsDataGrid.Columns)
            if (ModSortHelper.SortMemberMatches(column.SortMemberPath, sortMemberPath))
                column.SortDirection = direction;
            else
                column.SortDirection = null;
    }

    private void ClearColumnSortIndicators()
    {
        if (ModsDataGrid == null) return;

        foreach (var column in ModsDataGrid.Columns) column.SortDirection = null;
    }

    private void ModsDataGrid_OnSorting(object sender, DataGridSortingEventArgs e)
    {
        if (_viewModel is null) return;

        var sortMemberPath = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(sortMemberPath))
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;

        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        ApplyModListSort(sortMemberPath, direction, true);
    }
}
