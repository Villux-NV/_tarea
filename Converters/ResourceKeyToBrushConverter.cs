using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;

namespace Tarea.Converters;

public class ResourceKeyToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key
            && Avalonia.Application.Current!.Resources.TryGetResource(key, null, out var resource)
            && resource is ISolidColorBrush brush)
            return brush;

        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}