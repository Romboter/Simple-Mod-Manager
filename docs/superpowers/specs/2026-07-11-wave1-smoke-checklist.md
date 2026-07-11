# Wave 1 Manual Smoke Checklist (Area D backups, dialog-seam three-way confirm, dialog-seam OK/Cancel confirm)

Consolidated checklist for the three Wave 1 branches merged into `master` on 2026-07-11 (`d9ae7cd` Area D, `ff78094` three-way confirm, `5f4b172` OK/Cancel confirm). Run the built app (`dotnet run --project VintageStoryModManager -c Release`) and work through each section — none of these have been exercised by hand yet (see Verification status at the bottom).

## Area D — Backups

Requires a real VintagestoryData folder configured.

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

## Dialog seam — three-way confirm (`ConfirmThreeWayAsync`)

Drag a `.json` modlist file onto the window (or use File > Load Modlist):

- [ ] The "How would you like to load the modlist?" dialog still shows the same three buttons with the same labels ("Only Modlist mods" / "Add Modlist mods" / Cancel) and behaves identically to before.
- [ ] With `SuppressModlistSavePrompt` off, loading in Replace mode shows the backup-prompt dialog with the "No, don't ask again" button; clicking it both proceeds as "No" and persists the suppression (verify a second Replace-mode load no longer shows this prompt).
- [ ] Cancelling either dialog aborts the load with no changes.

## Dialog seam — OK/Cancel confirm (`ConfirmOkCancelAsync`)

Needs a state with no existing `firebase-auth.json` to exercise the first-time-consent path (back up and delete it from the config folder first if one already exists on your machine, then restore it after).

- [ ] Switching to the Online Modlists tab for the first time still shows the Firebase consent dialog with "No thanks" as the Cancel button label; clicking it reverts the tab selection to Local, same as before.
- [ ] Accepting consent proceeds to load online modlists.
- [ ] File > Restore firebase-auth.json backup still prompts and behaves identically to before.
- [ ] Submitting a mod compatibility vote for the first time (with no `firebase-auth.json`) still shows its own consent-dialog text.

## Verification status (as of the Wave 1 merge)

Three separate worktree agents ran these three branches through their full plans. All confirmed:

- Release build: 0 warnings/0 errors (build re-verified again after all three were merged into `master`).
- Full test suite: 411/411 passing (394 baseline + 12 Area D + 3 three-way + 2 OK/Cancel).
- The app launches from the Release build with no crash on startup.

None of the checklist items above were exercised interactively by any agent — this environment has no GUI-automation/click tooling for this native WPF app (confirmed independently three times; see the `no-wpf-gui-automation` project memory). The underlying logic for all three areas is either a direct mechanical port of the original dialog calls (three-way/OK-Cancel confirms) or covered by new unit tests exercising the same decision branches (Area D's coordinators — `ValidateRestore`, `ResolveInstalledVersionForDelete`, `GetBackupsForRestoreMenu`, `CreateBackupAsync`, `LoadBackupForRestore`, `ListBackupFiles`), but none of that substitutes for walking through the actual UI. **A human needs to do that before treating Wave 1 as fully verified.**
