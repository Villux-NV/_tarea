using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Tarea.Models;

namespace Tarea.Converters;

public class UrgencyToBrushConverter : IValueConverter
{
    public static AppSettings? Settings { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is CardUrgency urgency && urgency != CardUrgency.None)
        {
            var hex = urgency switch
            {
                CardUrgency.Low => Settings?.UrgencyLowColor ?? "#18FBA824",
                CardUrgency.Medium => Settings?.UrgencyMediumColor ?? "#22FC5B13",
                CardUrgency.High => Settings?.UrgencyHighColor ?? "#2AEF4444",
                _ => null
            };

            if (hex != null)
            {
                try
                {
                    var color = Color.Parse(hex);
                    return new SolidColorBrush(color);
                }
                catch { }
            }
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}