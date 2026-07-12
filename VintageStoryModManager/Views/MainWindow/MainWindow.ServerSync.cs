#nullable enable

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void OnServerOptionsEnabledChanged(bool isEnabled)
    {
        var singleSelection = _selectedMods.Count == 1 ? _selectedMods[0] : null;
        UpdateSelectedModCopyForServerButton(isEnabled ? singleSelection : null);
    }
}
