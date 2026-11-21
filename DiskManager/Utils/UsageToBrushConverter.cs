using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DiskManager.Utils;

public sealed class UsageToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var percent = value is double number ? number : 0d;
        return percent >= 90
            ? new SolidColorBrush(Color.FromRgb(0x8C, 0x3A, 0x35))
            : new SolidColorBrush(Color.FromRgb(0x1F, 0x1E, 0x1D));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}