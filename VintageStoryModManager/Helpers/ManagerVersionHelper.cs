#nullable enable

using System.Reflection;
using VintageStoryModManager.Views;

namespace VintageStoryModManager.Helpers;

internal static class ManagerVersionHelper
{
    internal static string? GetManagerInformationalVersion()
    {
        try
        {
            var assembly = typeof(MainWindow).Assembly;
            if (assembly is null) return null;

            var informationalVersion = assembly
                                           .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                                           .InformationalVersion
                                       ?? assembly.GetName().Version?.ToString();

            if (string.IsNullOrWhiteSpace(informationalVersion)) return null;

            var buildMetadataSeparatorIndex = informationalVersion.IndexOf('+');
            if (buildMetadataSeparatorIndex >= 0)
                informationalVersion = informationalVersion[..buildMetadataSeparatorIndex];

            return informationalVersion.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
