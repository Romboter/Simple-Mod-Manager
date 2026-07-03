#nullable enable
using SimpleVsManager.Cloud;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void SetUsernameDisplay(string? name)
    {
        var sanitized = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        _userConfiguration.SetCloudUploaderName(sanitized);
    }

    private string ResolveUploaderName(string? fallbackUserId = null)
    {
        var playerName = _viewModel?.PlayerName;
        if (!string.IsNullOrWhiteSpace(playerName)) return playerName.Trim();

        var suffixSource = _viewModel?.PlayerUid;
        if (string.IsNullOrWhiteSpace(suffixSource)) suffixSource = fallbackUserId;

        if (string.IsNullOrWhiteSpace(suffixSource)) suffixSource = _cloudModlistStore?.CurrentUserId;

        if (!string.IsNullOrWhiteSpace(suffixSource))
        {
            var trimmedSpan = suffixSource.AsSpan().Trim();
            if (!trimmedSpan.IsEmpty)
            {
                var suffix = trimmedSpan.Length <= 4
                    ? trimmedSpan.ToString()
                    : trimmedSpan.Slice(trimmedSpan.Length - 4, 4).ToString();

                if (string.IsNullOrWhiteSpace(suffix)) suffix = "0000";

                return $"Anonymous{suffix}";
            }
        }

        return "Anonymous0000";
    }

    private void ApplyPlayerIdentityToUiAndCloudStore()
    {
        SetUsernameDisplay(ResolveUploaderName());

        if (_cloudModlistStore is not null) ApplyPlayerIdentityToCloudStore(_cloudModlistStore);
    }

    private void ApplyPlayerIdentityToCloudStore(FirebaseModlistStore? store)
    {
        if (store is null) return;

        store.SetPlayerIdentity(_viewModel?.PlayerUid, _viewModel?.PlayerName);
    }

    private void SaveUploaderName()
    {
        if (_userConfiguration is null) return;

        var uploader = ResolveUploaderName(_cloudModlistStore?.CurrentUserId);
        SetUsernameDisplay(uploader);
    }

    private string DetermineUploaderName(FirebaseModlistStore store)
    {
        var uploader = ResolveUploaderName(store?.CurrentUserId);
        SetUsernameDisplay(uploader);
        return uploader;
    }
}
