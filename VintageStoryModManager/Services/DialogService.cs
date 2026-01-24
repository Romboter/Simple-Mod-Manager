using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using VintageStoryModManager.Models;
using VintageStoryModManager.Views.Dialogs;
using WpfMessageBox = VintageStoryModManager.Services.ModManagerMessageBox;

namespace VintageStoryModManager.Services;

/// <summary>
/// Service for creating and showing common dialogs in a consistent manner.
/// </summary>
public sealed class DialogService
{
    /// <summary>
    /// Shows a save modlist dialog and returns the user's input.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="suggestedName">Suggested modlist name</param>
    /// <param name="configOptions">Available configuration options</param>
    /// <param name="uploaderName">Default uploader name</param>
    /// <param name="defaultGameVersion">Default game version</param>
    /// <returns>The dialog result, or null if cancelled</returns>
    public SaveModlistDialogResult? ShowSaveModlistDialog(
        Window owner,
        string? suggestedName,
        List<ModConfigOption> configOptions,
        string uploaderName,
        string? defaultGameVersion)
    {
        var dialog = new SaveInstalledModsDialog(
            suggestedName,
            configOptions,
            uploaderName,
            defaultVersion: null,
            defaultGameVersion: defaultGameVersion,
            SaveInstalledModsDialogResult.SaveJson)
        {
            Owner = owner
        };

        var result = dialog.ShowDialog();
        if (result != true) return null;

        return new SaveModlistDialogResult(
            dialog.ListName,
            dialog.Version,
            dialog.Description,
            dialog.CreatedBy,
            dialog.VintageStoryVersion,
            dialog.SelectedAction,
            dialog.GetSelectedConfigOptions());
    }

    /// <summary>
    /// Shows a cloud modlist details dialog for uploading to cloud.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="suggestedName">Suggested modlist name</param>
    /// <param name="configOptions">Available configuration options</param>
    /// <param name="defaultGameVersion">Default game version</param>
    /// <returns>The dialog result, or null if cancelled</returns>
    public CloudModlistDetailsDialogResult? ShowCloudModlistDetailsDialog(
        Window owner,
        string suggestedName,
        List<ModConfigOption> configOptions,
        string? defaultGameVersion)
    {
        var dialog = new CloudModlistDetailsDialog(
            owner,
            suggestedName,
            configOptions,
            defaultGameVersion);

        var result = dialog.ShowDialog();
        if (result != true) return null;

        return new CloudModlistDetailsDialogResult(
            dialog.ModlistName,
            dialog.ModlistDescription,
            dialog.ModlistVersion,
            dialog.ModlistGameVersion,
            dialog.GetSelectedConfigOptions());
    }

    /// <summary>
    /// Shows a cloud slot selection dialog for choosing which cloud slot to use.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="slots">Available cloud slots</param>
    /// <param name="title">Dialog title</param>
    /// <param name="message">Dialog message</param>
    /// <returns>The selected slot, or null if cancelled</returns>
    public CloudModlistSlot? ShowCloudSlotSelectionDialog(
        Window owner,
        IReadOnlyList<CloudModlistSlot> slots,
        string title,
        string message)
    {
        var dialog = new CloudSlotSelectionDialog(owner, slots, title, message);
        var result = dialog.ShowDialog();
        return result == true ? dialog.SelectedSlot : null;
    }

    /// <summary>
    /// Shows a confirmation message box.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="message">The confirmation message</param>
    /// <param name="title">The dialog title</param>
    /// <param name="button">The button configuration</param>
    /// <param name="icon">The icon to display</param>
    /// <returns>The user's choice</returns>
    public MessageBoxResult ShowConfirmation(
        Window? owner,
        string message,
        string title,
        MessageBoxButton button = MessageBoxButton.YesNo,
        MessageBoxImage icon = MessageBoxImage.Question)
    {
        return owner is not null
            ? WpfMessageBox.Show(owner, message, title, button, icon)
            : WpfMessageBox.Show(message, title, button, icon);
    }

    /// <summary>
    /// Shows an error message box.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="message">The error message</param>
    /// <param name="title">The dialog title</param>
    public void ShowError(
        Window? owner,
        string message,
        string title = "Simple VS Manager")
    {
        if (owner is not null)
            WpfMessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        else
            WpfMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// Shows an information message box.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="message">The information message</param>
    /// <param name="title">The dialog title</param>
    public void ShowInformation(
        Window? owner,
        string message,
        string title = "Simple VS Manager")
    {
        if (owner is not null)
            WpfMessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        else
            WpfMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Shows a warning message box.
    /// </summary>
    /// <param name="owner">The owner window</param>
    /// <param name="message">The warning message</param>
    /// <param name="title">The dialog title</param>
    public void ShowWarning(
        Window? owner,
        string message,
        string title = "Simple VS Manager")
    {
        if (owner is not null)
            WpfMessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        else
            WpfMessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

/// <summary>
/// Result from the save modlist dialog.
/// </summary>
public sealed record SaveModlistDialogResult(
    string ListName,
    string? Version,
    string? Description,
    string? CreatedBy,
    string? VintageStoryVersion,
    SaveInstalledModsDialogResult SelectedAction,
    IReadOnlyList<ModConfigOption> SelectedConfigOptions);

/// <summary>
/// Result from the cloud modlist details dialog.
/// </summary>
public sealed record CloudModlistDetailsDialogResult(
    string ModlistName,
    string? ModlistDescription,
    string? ModlistVersion,
    string? ModlistGameVersion,
    IReadOnlyList<ModConfigOption> SelectedConfigOptions);
