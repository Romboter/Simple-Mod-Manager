# Area C (Modlist Workflow) Manual Smoke Checklist

Run the built app (`dotnet run --project VintageStoryModManager -c Release`) with a real installed-mods set.

- [ ] File > Save Modlist... prompts the metadata dialog, then writes a new `.json` file to the modlist folder and reports "Saved modlist" status.
- [ ] Saving a modlist with a name matching an existing file prompts "Replace Modlist" and only overwrites on confirmation.
- [ ] The Local Modlists tab's "Save" button (`SaveLocalModlistButton`) produces the same result and refreshes the grid with the new entry selected.
- [ ] File > Save Installed Mods as PDF... prompts the metadata dialog, writes a `.pdf` file, and reports "Saved installed mods PDF" status.
- [ ] Saving a PDF with a name matching an existing file prompts "Replace Modlist PDF" and only overwrites on confirmation.
- [ ] Cloud > Save current mods to cloud (area G, `SaveModlistToCloudAsync`) still works unchanged — confirms `TryBuildCurrentModlistJson`'s call site wasn't broken by the internal rewire.
- [ ] A modlist saved with included mod configurations round-trips those configurations correctly (spot-check one mod with a config file).

Work through each item by hand; check them off as they pass.
