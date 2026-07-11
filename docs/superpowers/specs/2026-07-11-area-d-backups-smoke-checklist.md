# Area D (Backups) Manual Smoke Checklist

Run the built app (`dotnet run --project VintageStoryModManager -c Release`) with a real VintagestoryData folder configured.

- [ ] File > Restore VintagestoryData folder backup submenu opens and lists existing backups for the current data directory only.
- [ ] "Delete all data folder backups" is disabled when no VS installation/version is detected, enabled otherwise.
- [ ] Deleting data-folder backups prompts a confirmation, then reports the deleted count.
- [ ] Restoring a data-folder backup with a mismatched VintageStory version shows the version-mismatch warning and does not restore.
- [ ] Restoring a data-folder backup for the current directory/version succeeds, shows the progress overlay, and reports success.
- [ ] "Change backup location..." updates where new data-folder backups are written.
- [ ] File > Restore modlist backup submenu lists JSON backups newest-first, with only the single newest "AppStarted" backup shown.
- [ ] Restoring a modlist backup applies the preset and reports status.
- [ ] Launching the game creates an automatic data-folder backup (Area I cross-call still works, unchanged).
- [ ] Loading a modlist (Area C) creates an automatic modlist backup (Area C cross-call still works, unchanged).
- [ ] App startup creates an "AppStarted" modlist backup (Startup cross-call still works, unchanged).

## Verification status (this pass, agent-driven)

The agent that performed this refactor could launch the built Release exe directly (confirmed it starts
and stays running with a real VintagestoryData folder and a real VintageStory install configured via the
`VINTAGE_STORY` environment variable on the dev machine) but does not have interactive GUI-driving
capability in this environment (no click/screenshot automation available), so the checklist items above
were **not** exercised by hand during this pass. What *was* verified programmatically:

- App launches from the Release build with no crash on startup (process stayed alive with a real
  VintagestoryData folder and a real VS install present).
- All extracted coordinator logic (`ValidateRestore`, `ResolveInstalledVersionForDelete`,
  `GetBackupsForRestoreMenu`, `CreateBackupAsync`, `LoadBackupForRestore`, `ListBackupFiles`) is covered
  by 12 new unit tests (6 `DataFolderBackupCoordinatorTests` + 6 `ModlistBackupCoordinatorTests`), all
  passing, exercising the same decision paths the checklist items above cover from the UI.
- Release build is 0 warnings/0 errors; full test suite is 406/406 passing (394 baseline + 12 new).

**A human should still walk through the checklist items above by hand** before considering this smoke-tested
end to end - the unit tests cover the coordinator logic but not the actual WPF menu wiring, dialog text,
or overlay visuals.
