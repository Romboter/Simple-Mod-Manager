# View-State Coupling Report: UpdateModsAsync

Transitive closure over the MainWindow method-call graph (direct calls + callback/method-group references) starting from **UpdateModsAsync** (MainWindow.ModUpdateCommands.cs). 19 method(s) reachable; 15 UI-bound member(s) touched.

| UI-bound member | Kind | Hops | Reached from | Shortest call path |
|---|---|---:|---|---|
| _isModUpdateInProgress | busy/overlay/progress field | 0 | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) |
| DeleteCachedModsMenuItem | XAML-generated control/member | 1 | RefreshDeleteCachedModsMenuHeaderAsync (MainWindow.ManagerCache.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> RefreshDeleteCachedModsMenuHeaderAsync (MainWindow.ManagerCache.cs) |
| IsModlistInstallInProgress | XAML-generated control/member | 1 | BeginModlistInstallUi (MainWindow.Progress.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> BeginModlistInstallUi (MainWindow.Progress.cs) |
| ModlistInstallProgress | XAML-generated control/member | 1 | BeginModlistInstallUi (MainWindow.Progress.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> BeginModlistInstallUi (MainWindow.Progress.cs) |
| ModlistInstallStatusMessage | XAML-generated control/member | 1 | BeginModlistInstallUi (MainWindow.Progress.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> BeginModlistInstallUi (MainWindow.Progress.cs) |
| SelectedModDatabasePageButton | XAML-generated control/member | 1 | UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) |
| SelectedModDeleteButton | XAML-generated control/member | 1 | UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) |
| SelectedModUpdateButton | XAML-generated control/member | 1 | UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) |
| _selectedMods | XAML-generated control/member | 1 | UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) |
| _selectionAnchor | XAML-generated control/member | 1 | RefreshModsAsync (MainWindow.ModRefresh.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> RefreshModsAsync (MainWindow.ModRefresh.cs) |
| HasModlistDownloadSpeed | XAML-generated control/member | 2 | UpdateModlistDownloadSpeed (MainWindow.Progress.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> BeginModlistInstallUi (MainWindow.Progress.cs) -> UpdateModlistDownloadSpeed (MainWindow.Progress.cs) |
| ModlistDownloadSpeed | XAML-generated control/member | 2 | UpdateModlistDownloadSpeed (MainWindow.Progress.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> BeginModlistInstallUi (MainWindow.Progress.cs) -> UpdateModlistDownloadSpeed (MainWindow.Progress.cs) |
| SelectedModCopyForServerButton | XAML-generated control/member | 2 | UpdateSelectedModCopyForServerButton (MainWindow.ModGridSelection.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) -> UpdateSelectedModCopyForServerButton (MainWindow.ModGridSelection.cs) |
| SelectedModEditConfigButton | XAML-generated control/member | 2 | UpdateSelectedModEditConfigButton (MainWindow.ModGridSelection.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) -> UpdateSelectedModEditConfigButton (MainWindow.ModGridSelection.cs) |
| SelectedModFixButton | XAML-generated control/member | 2 | UpdateSelectedModFixButton (MainWindow.ModGridSelection.cs) | UpdateModsAsync (MainWindow.ModUpdateCommands.cs) -> UpdateSelectedModButtons (MainWindow.ModGridSelection.cs) -> UpdateSelectedModFixButton (MainWindow.ModGridSelection.cs) |
