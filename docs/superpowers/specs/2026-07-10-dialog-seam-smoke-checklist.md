# Dialog-Seam Smoke Test — Slices 34–42 (2026-07-10)

One app session covers everything. Each item exercises dialogs that were rerouted  
through `IConfirmationService` (themed `MessageDialogWindow`). Pass bar: the dialog  
appears themed, with the right title/text/severity icon, and Yes/No prompts branch  
correctly. Error-path dialogs that need real failures (disk/network) are marked  
*(optional)* — skip unless easy to trigger.

## S34 — GameLaunch (`16cb1ed`)

- [x] Launch Vintage Story normally (happy path, no dialog expected)
- [x] File > Set custom Vintage Story shortcut → pick a valid shortcut
- [x] Re-open Set custom shortcut with one already set → "clear the custom shortcut?" Yes/No prompt — test **both** answers
- [x] *(optional)* Launch with missing data folder / bad shortcut → warning dialog

## S35 — Compatibility (`404df8d`)

- [x] Check Mods Compatibility with internet access disabled → warning dialog
- [x] Check Mods Compatibility normally → version selection → results dialog
- [x] Experimental comp review with no mod selected → "Select a mod first!" info dialog
- [x] Experimental comp review with a mod selected → comments dialog (dynamic title)

## S36 — CloudAuth (`4fd3186`)

- [x] Restore firebase-auth backup menu item → result dialog (any of the four outcomes counts)
- [x] Confirm the OKCancel restore confirmation still shows first (intentionally unchanged)

## S37 — Presets (`d51a8f5`)

- [x] Save Mod Preset → happy path
- [x] Save Mod Preset → in the file dialog, navigate OUTSIDE the Presets folder and save → warning + save canceled (stays in dialog)
- [x] Load Preset → same out-of-folder rejection check
- [x] Load Preset → happy path

## S38 — Modlists (`9e1ed1c`, `5fa06a3`)

- [x] Save Modlist with a name that already exists → "Replace Modlist" Yes/No — test **both** answers
- [x] Save Modlist happy path → appears in local modlists
- [x] Local modlists tab: load, modify, open-Modlists-folder buttons
- [x] Load-modlist load-mode prompt (YesNoCancel) still shows unchanged (intentionally unrouted)
- [x] *(optional)* Load a deleted/moved local modlist entry → warning + list refresh

## S39 — ModConfig (`276ac43`)

- [x] Mods menu > Scan for mod configs → "No missing mod configuration files were found." (or assigned-configs summary)
- [x] Edit Config button on a mod with a config (happy path)
- [x] *(optional)* Edit Config on a mod whose stored config path is broken → error dialog + path cleared

## S40 — Profiles / Path selection (`ef01215`)

- [x] Create Game Profile → first-time OKCancel warning still shows unchanged (if not yet acknowledged), then create with a duplicate/invalid name → info dialog
- [x] Edit Game Profile with no active profile → "No active profile to edit."
- [x] *(optional)* File > Set Data Folder → pick an invalid folder → warning, loop re-prompts

## S41 — Refresh / Mod usage (`92bf385`)

- [x] Mod-usage prompt: submit "No issues" votes → "Thanks! Your No issues votes were submitted."
- [x] *(optional)* Any refresh-failure dialog (needs a real refresh error — skip if not reproducible)

## S42 — Odds and ends (`2eea12b`)

- [x] Help > Discord button (happy path; error dialog only if browser launch fails)
- [x] Manager update link / Mod database page menu item opens
- [x] Sync to Server with a failing preflight (e.g. no server configured) → preflight dialog with correct severity
- [x] Copy selected mod for server → result dialog
- [x] Data-folder backups: open backup directory + change backup location menu items
- [x] User reports view loads (error dialog only on load failure)
- [x] *(optional)* Configuration-migration prompt — only fires when a legacy config exists

## Sign-off

- [x] All exercised dialogs were themed (no OS-style message boxes)
- [x] No crashes, hangs, or double-dialogs anywhere above

Result: Pass (pass/fail + notes) — then update the roadmap's "Awaiting user  
smoke test" entries for slices 34–42.
