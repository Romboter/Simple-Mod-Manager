using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using System.IO;
using System.Windows;

namespace VintageStoryModManager.Helpers;

internal static class ModlistDropHelper
{
    internal static bool TryGetDroppedModlistFile(DragEventArgs e, out string? filePath)
        {
            filePath = null;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;

            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return false;

            foreach (var candidate in files)
                if (HasSupportedModlistExtension(candidate))
                {
                    filePath = candidate;
                    return true;
                }

            return false;
        }

    internal static bool HasSupportedModlistExtension(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            return string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
        }
}
