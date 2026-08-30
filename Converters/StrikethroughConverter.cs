using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Tarea.Converters;

public class StrikethroughConverter : IValueConverter
{
    public static readonly StrikethroughConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isStrikethrough && isStrikethrough)
            return TextDecorations.Strikethrough;
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}