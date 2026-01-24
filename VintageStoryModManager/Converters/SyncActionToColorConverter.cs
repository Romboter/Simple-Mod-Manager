using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VintageStoryModManager.Models;

namespace VintageStoryModManager.Converters
{
    /// <summary>
    /// Converts SyncAction to a background color for display.
    /// </summary>
    public class SyncActionToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SyncAction action)
            {
                return action switch
                {
                    SyncAction.Upload => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4C, 0xAF, 0x50)), // Green
                    SyncAction.Skip => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0x9E)), // Gray
                    SyncAction.Delete => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF4, 0x43, 0x36)), // Red
                    SyncAction.CreateDirectory => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x21, 0x96, 0xF3)), // Blue
                    SyncAction.DeleteDirectory => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x98, 0x00)), // Orange
                    _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0x9E))
                };
            }
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9E, 0x9E, 0x9E));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
