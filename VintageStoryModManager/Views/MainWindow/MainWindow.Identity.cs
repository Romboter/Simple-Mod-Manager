#nullable enable
using SimpleVsManager.Cloud;
using VintageStoryModManager.Services;

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
        var suffixSource = _viewModel?.PlayerUid;
        if (string.IsNullOrWhiteSpace(suffixSource)) suffixSource = fallbackUserId;

        if (string.IsNullOrWhiteSpace(suffixSource)) suffixSource = _cloudModlistStore?.CurrentUserId;

        return UploaderNameResolver.Resolve(_viewModel?.PlayerName, suffixSource);
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
