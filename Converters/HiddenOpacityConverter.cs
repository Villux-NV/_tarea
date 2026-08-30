using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tarea.Converters;

public class HiddenOpacityConverter : IValueConverter
{
    public static readonly HiddenOpacityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 0.35 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}