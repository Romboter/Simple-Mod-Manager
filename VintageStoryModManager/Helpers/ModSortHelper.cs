using System.ComponentModel;
using VintageStoryModManager.ViewModels;

namespace VintageStoryModManager.Helpers;

internal static class ModSortHelper
{
    private static bool IsActiveSortMember(string? sortMemberPath)
        {
            if (string.IsNullOrWhiteSpace(sortMemberPath)) return false;

            return string.Equals(sortMemberPath, nameof(ModListItemViewModel.IsActive), StringComparison.OrdinalIgnoreCase)
                   || string.Equals(sortMemberPath, nameof(ModListItemViewModel.ActiveSortOrder),
                       StringComparison.OrdinalIgnoreCase);
        }

    internal static string NormalizeSortMemberPath(string sortMemberPath)
        {
            if (string.IsNullOrWhiteSpace(sortMemberPath)) return sortMemberPath;

            var trimmed = sortMemberPath.Trim();

            if (string.Equals(trimmed, nameof(ModListItemViewModel.DisplayName), StringComparison.OrdinalIgnoreCase))
                return nameof(ModListItemViewModel.NameSortKey);

            return IsActiveSortMember(trimmed)
                ? nameof(ModListItemViewModel.ActiveSortOrder)
                : trimmed;
        }

    internal static bool SortMemberMatches(string? columnSortMemberPath, string sortMemberPath)
        {
            if (string.IsNullOrWhiteSpace(columnSortMemberPath)) return false;

            return string.Equals(
                NormalizeSortMemberPath(columnSortMemberPath),
                NormalizeSortMemberPath(sortMemberPath),
                StringComparison.OrdinalIgnoreCase);
        }

    internal static bool SortOptionMatches(SortOption option, string sortMemberPath, ListSortDirection direction)
        {
            if (option.SortDescriptions.Count == 0) return false;

            var primary = option.SortDescriptions[0];
            if (!string.Equals(primary.Property, sortMemberPath, StringComparison.OrdinalIgnoreCase)
                || primary.Direction != direction)
                return false;

            if (IsActiveSortMember(sortMemberPath))
            {
                if (option.SortDescriptions.Count < 2) return false;

                var secondary = option.SortDescriptions[1];
                return string.Equals(secondary.Property, nameof(ModListItemViewModel.NameSortKey),
                           StringComparison.OrdinalIgnoreCase)
                       && secondary.Direction == ListSortDirection.Ascending;
            }

            return true;
        }

    internal static (string Property, ListSortDirection Direction)[] BuildSortDescriptions(string sortMemberPath,
            ListSortDirection direction)
        {
            List<(string Property, ListSortDirection Direction)> sorts;

            if (IsActiveSortMember(sortMemberPath))
                sorts = new List<(string, ListSortDirection)>
                {
                    (nameof(ModListItemViewModel.ActiveSortOrder), direction),
                    (nameof(ModListItemViewModel.NameSortKey), ListSortDirection.Ascending)
                };
            else if (string.Equals(sortMemberPath, nameof(ModListItemViewModel.LatestVersionSortKey),
                         StringComparison.OrdinalIgnoreCase))
                sorts = new List<(string, ListSortDirection)>
                {
                    (nameof(ModListItemViewModel.LatestVersionSortKey), direction),
                    (nameof(ModListItemViewModel.NameSortKey), ListSortDirection.Ascending)
                };
            else
                sorts = new List<(string, ListSortDirection)>
                {
                    (sortMemberPath, direction)
                };

            return sorts.ToArray();
        }

    internal static string BuildSortDisplayName(string sortMemberPath, ListSortDirection direction)
        {
            if (IsActiveSortMember(sortMemberPath))
                return direction == ListSortDirection.Ascending
                    ? "Active (Active → Inactive)"
                    : "Active (Inactive → Active)";

            if (string.Equals(sortMemberPath, nameof(ModListItemViewModel.NameSortKey), StringComparison.OrdinalIgnoreCase))
                return direction == ListSortDirection.Ascending
                    ? "Name (A → Z)"
                    : "Name (Z → A)";

            if (string.Equals(sortMemberPath, nameof(ModListItemViewModel.LatestVersionSortKey),
                    StringComparison.OrdinalIgnoreCase))
                return direction == ListSortDirection.Ascending
                    ? "Latest Version (Updates First)"
                    : "Latest Version (Updates Last)";

            return $"{sortMemberPath} ({(direction == ListSortDirection.Ascending ? "Ascending" : "Descending")})";
        }
}
