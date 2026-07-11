# Simple VS Manager MainWindow Menu Map

Generated from `VintageStoryModManager\Views\MainWindow.xaml` on branch `refactor/mainwindow-service-extractions` at commit `f4248fe`.

Notes:
- WPF `_` access-key markers are removed in display paths. Example: `_File` becomes `File`.
- `Click=` is the code-behind event handler used by the menu item.
- This file is intentionally untracked/operator documentation unless you choose to stage it.

## Full menu tree

- **File** — line `107`
  - **Set Data Folder...** — Click=`SelectDataFolderMenuItem_OnClick`; line `113`
  - **Set Game Folder...** — Click=`SelectGameFolderMenuItem_OnClick`; line `117`
  - **Game Profiles** — x:Name=`GameProfilesMenuItem`; line `121`
    - **Create new...** — x:Name=`CreateGameProfileMenuItem`; Click=`CreateGameProfileMenuItem_OnClick`; line `126`
    - **Edit current...** — x:Name=`EditGameProfileMenuItem`; Click=`EditGameProfileMenuItem_OnClick`; line `131`
    - **Delete profile...** — x:Name=`DeleteGameProfileMenuItem`; Click=`DeleteGameProfileMenuItem_OnClick`; line `136`
  - **Restore mods backup** — line `145`
    - **Loading...** — Enabled=`False`; line `149`
  - **Restore VintagestoryData backup** — x:Name=`RestoreDataFolderMenuItem`; line `151`
    - **Loading...** — Enabled=`False`; line `156`
  - **Automatically back up VintagestoryData before launch** — x:Name=`AutomaticDataBackupsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.AutomaticDataBackupsEnabled, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `158`
  - `--- separator ---` line `166`
  - **Server Targets...** — x:Name=`ManageServerTargetsMenuItem`; Click=`ManageServerTargetsMenuItem_OnClick`; line `167`
  - **Sync to Server...** — x:Name=`SyncToServerMenuItem`; Click=`SyncToServerMenuItem_OnClick`; Enabled=`False`; line `172`
  - `--- separator ---` line `178`
  - **Exit** — Click=`ExitMenuItem_OnClick`; line `179`
- **Mods** — line `184`
  - **Scan for mod config files** — Click=`ScanForModConfigsMenuItem_OnClick`; line `190`
  - **Check mods compatibility** — Click=`CheckModsCompatibilityMenuItem_OnClick`; line `194`
  - **Require exact VS version for compatibility checks** — x:Name=`RequireExactVsVersionMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.RequireExactVsVersionMatch, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `198`
  - **Cache all versions locally** — x:Name=`CacheAllVersionsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.CacheAllVersionsLocally, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `205`
  - **Delete Cached Mods** — x:Name=`DeleteCachedModsMenuItem`; Click=`DeleteCachedModsMenuItem_OnClick`; line `212`
- **Folders** — line `218`
  - **Open Mods folder** — Click=`OpenModFolderButton_OnClick`; line `223`
  - **Open Logs folder** — Click=`OpenLogsFolderButton_OnClick`; line `227`
  - **Open Config folder** — Click=`OpenConfigFolderButton_OnClick`; line `231`
  - **Open Modlists folder** — Click=`OpenModlistsFolderButton_OnClick`; line `235`
  - **Open Simple VS Manager folder** — Click=`ManagerDataFolderMenuItem_OnClick`; line `239`
- **Save & Load** — x:Name=`PresetsAndModlistsMenuItem`; line `244`
  - **Save Modlist...** — Click=`SaveModlistMenuItem_OnClick`; line `250`
  - **Load Modlist...** — Click=`LoadModlistMenuItem_OnClick`; line `254`
  - `--- separator ---` line `258`
  - **Save Preset...** — Click=`SavePresetMenuItem_OnClick`; line `259`
  - **Load Preset** — x:Name=`LoadPresetMenuItem`; line `263`
    - **Browse...** — x:Name=`BrowsePresetMenuItem`; Click=`LoadPresetMenuItem_OnClick`; line `268`
  - `--- separator ---` line `274`
  - **Always clear mods before loading Modlist** — x:Name=`AlwaysClearModlistsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.AlwaysClearModlists, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `275`
  - **Always add to current mods when loading Modlist** — x:Name=`AlwaysAddModlistsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.AlwaysAddModlists, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `282`
- **Performance** — x:Name=`PerformanceMenuItem`; line `290`
  - **Disable auto-refresh (Faster startup, use manual Refresh button)** — x:Name=`DisableAutoRefreshMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.DisableAutoRefresh, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `296`
  - **Faster thumbnails (Faster to load in mod database)** — x:Name=`UseFasterThumbnailsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.UseFasterThumbnails, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `304`
  - **Disable hover and transition visual effects** — x:Name=`DisableHoverEffectsMenuItem`; Click=`DisableHoverEffectsMenuItem_OnClick`; Checkable=`True`; line `311`
- **View** — line `319`
  - **Compact View** — Checkable=`True`; Checked=`{Binding IsCompactView, Mode=TwoWay}`; line `324`
  - **Themes** — x:Name=`ThemesMenuItem`; line `330`
    - **Vintage Story** — x:Name=`VintageStoryThemeMenuItem`; Checkable=`True`; Checked=`{Binding ThemeMenu.IsVintageStorySelected, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `331`
    - **Dark** — x:Name=`DarkThemeMenuItem`; Checkable=`True`; Checked=`{Binding ThemeMenu.IsDarkSelected, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `340`
    - **Light** — x:Name=`LightThemeMenuItem`; Checkable=`True`; Checked=`{Binding ThemeMenu.IsLightSelected, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `349`
    - `--- separator ---` line `358`
    - **Edit theme...** — x:Name=`EditThemeMenuItem`; Click=`EditThemeMenuItem_OnClick`; line `359`
  - **Columns** — x:Name=`ColumnsMenuItem`; Visibility=`{Binding IsViewingMainTab, Converter={StaticResource IMM.BooleanToVisibilityConverter}}`; line `365`
    - **Active** — x:Name=`ActiveColumnMenuItem`; Checkable=`True`; Checked=`True`; line `370`
    - **Icon** — x:Name=`IconColumnMenuItem`; Checkable=`True`; Checked=`True`; line `378`
    - **Name** — x:Name=`NameColumnMenuItem`; Checkable=`True`; Checked=`True`; line `386`
    - **Version** — x:Name=`VersionColumnMenuItem`; Checkable=`True`; Checked=`True`; line `394`
    - **Latest Version** — x:Name=`LatestVersionColumnMenuItem`; Checkable=`True`; Checked=`True`; line `402`
    - **Authors** — x:Name=`AuthorsColumnMenuItem`; Checkable=`True`; Checked=`True`; line `410`
    - **Tags** — x:Name=`TagsColumnMenuItem`; Checkable=`True`; Checked=`True`; line `418`
    - **Status** — x:Name=`StatusColumnMenuItem`; Checkable=`True`; Checked=`True`; line `426`
    - **User Reports** — x:Name=`UserReportsColumnMenuItem`; Checkable=`True`; Checked=`True`; line `434`
    - **Side** — x:Name=`SideColumnMenuItem`; Checkable=`True`; Checked=`True`; line `442`
- **Help & About** — line `452`
  - **Version: --** — x:Name=`ManagerVersionMenuItem`; Enabled=`False`; line `457`
  - **Vintage Story: --** — x:Name=`GameVersionMenuItem`; Visibility=`Collapsed`; Enabled=`False`; line `464`
  - `--- separator ---` line `472`
  - **Menu Guide** — Click=`GuideMenuItem_OnClick`; line `473`
  - **Help!** — Click=`HelpMenuItem_OnClick`; line `477`
  - **Open Mod DB page for the manager** — Click=`ManagerModDbPageMenuItem_OnClick`; line `481`
  - **Clear temporary cache (Safe to use, nothing important is removed)** — Click=`ClearAllCachesMenuItem_OnClick`; line `485`
- **Advanced** — line `490`
  - **Set Custom Vintage Story Shortcut** — Click=`SetCustomShortcutMenuItem_OnClick`; line `495`
  - **Developer Profiles** — x:Name=`DeveloperProfilesMenuItem`; Visibility=`Collapsed`; line `499`
  - **Change manager config and cache folder...** — Click=`ChangeManagerFolderMenuItem_OnClick`; line `504`
  - **Restore firebase-auth.json using backup** — Click=`RestoreFirebaseAuthBackupMenuItem_OnClick`; line `508`
  - **Disable Internet Access** — x:Name=`DisableInternetAccessMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.DisableInternetAccess, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `512`
  - **Enable server options** — x:Name=`EnableServerOptionsMenuItem`; Click=`EnableServerOptionsMenuItem_OnClick`; Checkable=`True`; line `519`
  - **Enable logging for...** — line `527`
    - **Mod update** — x:Name=`LogModUpdateMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogModUpdates, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `528`
    - **Mod installation** — x:Name=`LogModInstallMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogModInstalls, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `535`
    - **Mod deletion** — x:Name=`LogModDeletionMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogModDeletions, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `542`
    - **App launch and exit** — x:Name=`LogAppLifecycleMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogAppLaunchAndExit, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `549`
    - **Errors and exceptions** — x:Name=`LogErrorsAndExceptionsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogErrorsAndExceptions, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `556`
  - **Debug: Show all game log lines referencing the selected mod.** — Click=`ExperimentalModDebuggingMenuItem_OnClick`; line `564`
  - **Debug: Show all game log lines found referencing any installed mod** — Click=`ExperimentalAllModsDebuggingMenuItem_OnClick`; line `568`
  - `--- separator ---` line `572`
  - **Delete all Simple VS Manager files (Uninstall)** — Click=`DeleteAllManagerFilesMenuItem_OnClick`; line `573`

## Click-handler lookup

- `ChangeManagerFolderMenuItem_OnClick` → **Advanced → Change manager config and cache folder...** — line `504`
- `ExperimentalAllModsDebuggingMenuItem_OnClick` → **Advanced → Debug: Show all game log lines found referencing any installed mod** — line `568`
- `ExperimentalModDebuggingMenuItem_OnClick` → **Advanced → Debug: Show all game log lines referencing the selected mod.** — line `564`
- `DeleteAllManagerFilesMenuItem_OnClick` → **Advanced → Delete all Simple VS Manager files (Uninstall)** — line `573`
- `EnableServerOptionsMenuItem_OnClick` → **Advanced → Enable server options** — line `519`
- `RestoreFirebaseAuthBackupMenuItem_OnClick` → **Advanced → Restore firebase-auth.json using backup** — line `508`
- `SetCustomShortcutMenuItem_OnClick` → **Advanced → Set Custom Vintage Story Shortcut** — line `495`
- `ExitMenuItem_OnClick` → **File → Exit** — line `179`
- `CreateGameProfileMenuItem_OnClick` → **File → Game Profiles → Create new...** — line `126`
- `DeleteGameProfileMenuItem_OnClick` → **File → Game Profiles → Delete profile...** — line `136`
- `EditGameProfileMenuItem_OnClick` → **File → Game Profiles → Edit current...** — line `131`
- `ManageServerTargetsMenuItem_OnClick` → **File → Server Targets...** — line `167`
- `SelectDataFolderMenuItem_OnClick` → **File → Set Data Folder...** — line `113`
- `SelectGameFolderMenuItem_OnClick` → **File → Set Game Folder...** — line `117`
- `SyncToServerMenuItem_OnClick` → **File → Sync to Server...** — line `172`
- `OpenConfigFolderButton_OnClick` → **Folders → Open Config folder** — line `231`
- `OpenLogsFolderButton_OnClick` → **Folders → Open Logs folder** — line `227`
- `OpenModlistsFolderButton_OnClick` → **Folders → Open Modlists folder** — line `235`
- `OpenModFolderButton_OnClick` → **Folders → Open Mods folder** — line `223`
- `ManagerDataFolderMenuItem_OnClick` → **Folders → Open Simple VS Manager folder** — line `239`
- `ClearAllCachesMenuItem_OnClick` → **Help & About → Clear temporary cache (Safe to use, nothing important is removed)** — line `485`
- `HelpMenuItem_OnClick` → **Help & About → Help!** — line `477`
- `GuideMenuItem_OnClick` → **Help & About → Menu Guide** — line `473`
- `ManagerModDbPageMenuItem_OnClick` → **Help & About → Open Mod DB page for the manager** — line `481`
- `CheckModsCompatibilityMenuItem_OnClick` → **Mods → Check mods compatibility** — line `194`
- `DeleteCachedModsMenuItem_OnClick` → **Mods → Delete Cached Mods** — line `212`
- `ScanForModConfigsMenuItem_OnClick` → **Mods → Scan for mod config files** — line `190`
- `DisableHoverEffectsMenuItem_OnClick` → **Performance → Disable hover and transition visual effects** — line `311`
- `LoadModlistMenuItem_OnClick` → **Save & Load → Load Modlist...** — line `254`
- `LoadPresetMenuItem_OnClick` → **Save & Load → Load Preset → Browse...** — line `268`
- `SaveModlistMenuItem_OnClick` → **Save & Load → Save Modlist...** — line `250`
- `SavePresetMenuItem_OnClick` → **Save & Load → Save Preset...** — line `259`
- `EditThemeMenuItem_OnClick` → **View → Themes → Edit theme...** — line `359`

## Named menu-item lookup

- `ActiveColumnMenuItem` → **View → Columns → Active** — line `370`
- `AlwaysAddModlistsMenuItem` → **Save & Load → Always add to current mods when loading Modlist** — line `282`
- `AlwaysClearModlistsMenuItem` → **Save & Load → Always clear mods before loading Modlist** — line `275`
- `AuthorsColumnMenuItem` → **View → Columns → Authors** — line `410`
- `AutomaticDataBackupsMenuItem` → **File → Automatically back up VintagestoryData before launch** — line `158`
- `BrowsePresetMenuItem` → **Save & Load → Load Preset → Browse...** — line `268`
- `CacheAllVersionsMenuItem` → **Mods → Cache all versions locally** — line `205`
- `ColumnsMenuItem` → **View → Columns** — line `365`
- `CreateGameProfileMenuItem` → **File → Game Profiles → Create new...** — line `126`
- `DarkThemeMenuItem` → **View → Themes → Dark** — line `340`
- `DeleteCachedModsMenuItem` → **Mods → Delete Cached Mods** — line `212`
- `DeleteGameProfileMenuItem` → **File → Game Profiles → Delete profile...** — line `136`
- `DeveloperProfilesMenuItem` → **Advanced → Developer Profiles** — line `499`
- `DisableAutoRefreshMenuItem` → **Performance → Disable auto-refresh (Faster startup, use manual Refresh button)** — line `296`
- `DisableHoverEffectsMenuItem` → **Performance → Disable hover and transition visual effects** — line `311`
- `DisableInternetAccessMenuItem` → **Advanced → Disable Internet Access** — line `512`
- `EditGameProfileMenuItem` → **File → Game Profiles → Edit current...** — line `131`
- `EditThemeMenuItem` → **View → Themes → Edit theme...** — line `359`
- `EnableServerOptionsMenuItem` → **Advanced → Enable server options** — line `519`
- `GameProfilesMenuItem` → **File → Game Profiles** — line `121`
- `GameVersionMenuItem` → **Help & About → Vintage Story: --** — line `464`
- `IconColumnMenuItem` → **View → Columns → Icon** — line `378`
- `LatestVersionColumnMenuItem` → **View → Columns → Latest Version** — line `402`
- `LightThemeMenuItem` → **View → Themes → Light** — line `349`
- `LoadPresetMenuItem` → **Save & Load → Load Preset** — line `263`
- `LogAppLifecycleMenuItem` → **Advanced → Enable logging for... → App launch and exit** — line `549`
- `LogErrorsAndExceptionsMenuItem` → **Advanced → Enable logging for... → Errors and exceptions** — line `556`
- `LogModDeletionMenuItem` → **Advanced → Enable logging for... → Mod deletion** — line `542`
- `LogModInstallMenuItem` → **Advanced → Enable logging for... → Mod installation** — line `535`
- `LogModUpdateMenuItem` → **Advanced → Enable logging for... → Mod update** — line `528`
- `ManagerVersionMenuItem` → **Help & About → Version: --** — line `457`
- `ManageServerTargetsMenuItem` → **File → Server Targets...** — line `167`
- `NameColumnMenuItem` → **View → Columns → Name** — line `386`
- `PerformanceMenuItem` → **Performance** — line `290`
- `PresetsAndModlistsMenuItem` → **Save & Load** — line `244`
- `RequireExactVsVersionMenuItem` → **Mods → Require exact VS version for compatibility checks** — line `198`
- `RestoreDataFolderMenuItem` → **File → Restore VintagestoryData backup** — line `151`
- `SideColumnMenuItem` → **View → Columns → Side** — line `442`
- `StatusColumnMenuItem` → **View → Columns → Status** — line `426`
- `SyncToServerMenuItem` → **File → Sync to Server...** — line `172`
- `TagsColumnMenuItem` → **View → Columns → Tags** — line `418`
- `ThemesMenuItem` → **View → Themes** — line `330`
- `UseFasterThumbnailsMenuItem` → **Performance → Faster thumbnails (Faster to load in mod database)** — line `304`
- `UserReportsColumnMenuItem` → **View → Columns → User Reports** — line `434`
- `VersionColumnMenuItem` → **View → Columns → Version** — line `394`
- `VintageStoryThemeMenuItem` → **View → Themes → Vintage Story** — line `331`

## Checkable menu items

- **Advanced → Disable Internet Access** — x:Name=`DisableInternetAccessMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.DisableInternetAccess, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `512`
- **Advanced → Enable logging for... → App launch and exit** — x:Name=`LogAppLifecycleMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogAppLaunchAndExit, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `549`
- **Advanced → Enable logging for... → Errors and exceptions** — x:Name=`LogErrorsAndExceptionsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogErrorsAndExceptions, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `556`
- **Advanced → Enable logging for... → Mod deletion** — x:Name=`LogModDeletionMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogModDeletions, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `542`
- **Advanced → Enable logging for... → Mod installation** — x:Name=`LogModInstallMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogModInstalls, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `535`
- **Advanced → Enable logging for... → Mod update** — x:Name=`LogModUpdateMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.LogModUpdates, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `528`
- **Advanced → Enable server options** — x:Name=`EnableServerOptionsMenuItem`; Click=`EnableServerOptionsMenuItem_OnClick`; Checkable=`True`; line `519`
- **File → Automatically back up VintagestoryData before launch** — x:Name=`AutomaticDataBackupsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.AutomaticDataBackupsEnabled, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `158`
- **Mods → Cache all versions locally** — x:Name=`CacheAllVersionsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.CacheAllVersionsLocally, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `205`
- **Mods → Require exact VS version for compatibility checks** — x:Name=`RequireExactVsVersionMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.RequireExactVsVersionMatch, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `198`
- **Performance → Disable auto-refresh (Faster startup, use manual Refresh button)** — x:Name=`DisableAutoRefreshMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.DisableAutoRefresh, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `296`
- **Performance → Disable hover and transition visual effects** — x:Name=`DisableHoverEffectsMenuItem`; Click=`DisableHoverEffectsMenuItem_OnClick`; Checkable=`True`; line `311`
- **Performance → Faster thumbnails (Faster to load in mod database)** — x:Name=`UseFasterThumbnailsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.UseFasterThumbnails, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `304`
- **Save & Load → Always add to current mods when loading Modlist** — x:Name=`AlwaysAddModlistsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.AlwaysAddModlists, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `282`
- **Save & Load → Always clear mods before loading Modlist** — x:Name=`AlwaysClearModlistsMenuItem`; Checkable=`True`; Checked=`{Binding SettingsMenu.AlwaysClearModlists, RelativeSource={RelativeSource AncestorType=Window}, Mode=TwoWay}`; line `275`
- **View → Columns → Active** — x:Name=`ActiveColumnMenuItem`; Checkable=`True`; Checked=`True`; line `370`
- **View → Columns → Authors** — x:Name=`AuthorsColumnMenuItem`; Checkable=`True`; Checked=`True`; line `410`
- **View → Columns → Icon** — x:Name=`IconColumnMenuItem`; Checkable=`True`; Checked=`True`; line `378`
- **View → Columns → Latest Version** — x:Name=`LatestVersionColumnMenuItem`; Checkable=`True`; Checked=`True`; line `402`
- **View → Columns → Name** — x:Name=`NameColumnMenuItem`; Checkable=`True`; Checked=`True`; line `386`
- **View → Columns → Side** — x:Name=`SideColumnMenuItem`; Checkable=`True`; Checked=`True`; line `442`
- **View → Columns → Status** — x:Name=`StatusColumnMenuItem`; Checkable=`True`; Checked=`True`; line `426`
- **View → Columns → Tags** — x:Name=`TagsColumnMenuItem`; Checkable=`True`; Checked=`True`; line `418`
- **View → Columns → User Reports** — x:Name=`UserReportsColumnMenuItem`; Checkable=`True`; Checked=`True`; line `434`
- **View → Columns → Version** — x:Name=`VersionColumnMenuItem`; Checkable=`True`; Checked=`True`; line `394`
- **View → Compact View** — Checkable=`True`; Checked=`{Binding IsCompactView, Mode=TwoWay}`; line `324`
- **View → Themes → Dark** — x:Name=`DarkThemeMenuItem`; Checkable=`True`; Checked=`{Binding ThemeMenu.IsDarkSelected, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `340`
- **View → Themes → Light** — x:Name=`LightThemeMenuItem`; Checkable=`True`; Checked=`{Binding ThemeMenu.IsLightSelected, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `349`
- **View → Themes → Vintage Story** — x:Name=`VintageStoryThemeMenuItem`; Checkable=`True`; Checked=`{Binding ThemeMenu.IsVintageStorySelected, RelativeSource={RelativeSource AncestorType=Window}, Mode=OneWay}`; line `331`

## Hidden/collapsed menu items

- **Advanced → Developer Profiles** — x:Name=`DeveloperProfilesMenuItem`; Visibility=`Collapsed`; line `499`
- **Help & About → Vintage Story: --** — x:Name=`GameVersionMenuItem`; Visibility=`Collapsed`; Enabled=`False`; line `464`
- **View → Columns** — x:Name=`ColumnsMenuItem`; Visibility=`{Binding IsViewingMainTab, Converter={StaticResource IMM.BooleanToVisibilityConverter}}`; line `365`

## Current smoke-test landmarks

- **Folders → Open Simple VS Manager folder** — `ManagerDataFolderMenuItem_OnClick`, line `239`. Open the manager data folder.
- **Help & About → Clear temporary cache (Safe to use, nothing important is removed)** — `ClearAllCachesMenuItem_OnClick`, line `485`. Clear all temporary caches. Cancel unless intentionally clearing.
- **Advanced → Change manager config and cache folder...** — `ChangeManagerFolderMenuItem_OnClick`, line `504`. Open the custom manager-folder change dialog. Cancel before move/reset.
- **Advanced → Delete all Simple VS Manager files (Uninstall)** — `DeleteAllManagerFilesMenuItem_OnClick`, line `573`. Delete all manager-created files. Cancel unless intentionally wiping manager data.
- **Mods → Delete Cached Mods** — `DeleteCachedModsMenuItem_OnClick`, line `212`. Delete cached mods. Cancel unless intentionally deleting cached mods.
