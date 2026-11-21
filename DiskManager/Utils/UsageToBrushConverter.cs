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
        if (percent >= 90)
        {
            return Brushes.IndianRed;
        }

        if (percent >= 70)
        {
            return Brushes.DarkOrange;
        }

        return Brushes.SeaGreen;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}