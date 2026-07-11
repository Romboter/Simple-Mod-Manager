#nullable enable
using VintageStoryModManager.Services;

namespace VintageStoryModManager.Views;

public partial class MainWindow
{
    private void InitializeTraceListener()
    {
        // Remove existing listener if present
        if (_traceListener != null)
        {
            System.Diagnostics.Trace.Listeners.Remove(_traceListener);
            _traceListener.Dispose();
            _traceListener = null;
        }

        // Add new listener if logging is enabled
        if (_userConfiguration.LogErrorsAndExceptions)
        {
            _traceListener = new ModManagerTraceListener(_modActivityLoggingService);
            System.Diagnostics.Trace.Listeners.Add(_traceListener);
            _modActivityLoggingService.LogDiagnostic("Error and exception logging enabled");
        }
    }
}
