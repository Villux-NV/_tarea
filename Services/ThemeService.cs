using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Tarea.Models;

namespace Tarea.Services;

public static class ThemeService
{
    public static readonly string[] PaletteKeys =
        { "Black", "Slate", "Rose", "Muted", "Green", "Orange", "Yellow", "Blue" };

    public static readonly Dictionary<string, string> PaletteLabels = new()
    {
        ["Black"] = "background",
        ["Slate"] = "hover / subtle",
        ["Rose"] = "text / primary",
        ["Muted"] = "muted text",
        ["Green"] = "done / accent",
        ["Orange"] = "wip / danger",
        ["Yellow"] = "todo / warning",
        ["Blue"] = "link / highlight",
    };

    private static readonly Dictionary<ThemePreset, Dictionary<string, Color>> Themes = new()
    {
        // ── Rose: current default (rose on black) ──────────
        [ThemePreset.Rose] = new()
        {
            ["Black"] = Color.Parse("#0C0002"),
            ["Slate"] = Color.Parse("#0F172A"),
            ["Rose"] = Color.Parse("#E8B4B8"),
            ["Muted"] = Color.Parse("#665E5F"),
            ["Green"] = Color.Parse("#7B832D"),
            ["Orange"] = Color.Parse("#FC5B13"),
            ["Yellow"] = Color.Parse("#FBA824"),
            ["Blue"] = Color.Parse("#00B3B0"),
        },

        // ── Amber: warm CRT phosphor monitor ──────────────
        [ThemePreset.Amber] = new()
        {
            ["Black"] = Color.Parse("#080600"),
            ["Slate"] = Color.Parse("#161208"),
            ["Rose"] = Color.Parse("#FFB830"),    // amber phosphor glow
            ["Muted"] = Color.Parse("#7A6830"),
            ["Green"] = Color.Parse("#C8A830"),   // warm yellow-green
            ["Orange"] = Color.Parse("#FF8820"),  // bright amber-orange
            ["Yellow"] = Color.Parse("#FFD040"),  // golden
            ["Blue"] = Color.Parse("#E09020"),    // copper (no blue on an amber CRT)
        },

        // ── Integrale: Lancia Delta HF / Martini Racing ───
        [ThemePreset.Integrale] = new()
        {
            ["Black"] = Color.Parse("#080C18"),   // deep navy-black
            ["Slate"] = Color.Parse("#121A2E"),   // dark navy hover
            ["Rose"] = Color.Parse("#EDE8E0"),    // warm cream (white body panels)
            ["Muted"] = Color.Parse("#5C6478"),   // blue-gray
            ["Green"] = Color.Parse("#3A8A5C"),   // Italian racing green (muted)
            ["Orange"] = Color.Parse("#D42030"),  // Martini red
            ["Yellow"] = Color.Parse("#D4A828"),  // Integrale badge gold
            ["Blue"] = Color.Parse("#41B6E6"),    // Martini light blue
        },
    };


    // ── Public API ─────────────────────────────────────────────
    public static void Apply(ThemePreset preset, AppSettings? settings = null)
    {
        Dictionary<string, Color> colors;

        if (preset == ThemePreset.Custom && settings != null)
        {
            colors = new Dictionary<string, Color>();
            foreach (var key in PaletteKeys)
            {
                var hex = settings.CustomThemeColors.TryGetValue(key, out var val)
                    ? val
                    : "#FF00FF"; // magenta fallback — makes missing keys obvious
                colors[key] = Color.Parse(hex);
            }
        }
        else if (Themes.TryGetValue(preset, out var themeColors))
        {
            colors = themeColors;
        }
        else
        {
            return;
        }

        var res = Application.Current!.Resources;

        foreach (var (key, color) in colors)
        {
            res[$"{key}Brush"] = new SolidColorBrush(color);
        }

        res["StatusTodoBrush"] = new SolidColorBrush(colors["Yellow"]);
        res["StatusWipBrush"] = new SolidColorBrush(colors["Orange"]);
        res["StatusDoneBrush"] = new SolidColorBrush(colors["Green"]);
    }

    public static void ApplySaved(SavedTheme saved)
    {
        var colors = new Dictionary<string, Color>();
        foreach (var key in PaletteKeys)
        {
            var hex = saved.Colors.TryGetValue(key, out var val)
                ? val
                : "#FF00FF";
            colors[key] = Color.Parse(hex);
        }

        var res = Application.Current!.Resources;
        foreach (var (key, color) in colors)
            res[$"{key}Brush"] = new SolidColorBrush(color);

        res["StatusTodoBrush"] = new SolidColorBrush(colors["Yellow"]);
        res["StatusWipBrush"] = new SolidColorBrush(colors["Orange"]);
        res["StatusDoneBrush"] = new SolidColorBrush(colors["Green"]);
    }

    public static void ApplyFontSize(int size)
    {
        Application.Current!.Resources["BaseFontSize"] = (double)size;
    }

    public static Dictionary<string, string> GetPresetColors(ThemePreset preset)
    {
        if (Themes.TryGetValue(preset, out var colors))
        {
            var result = new Dictionary<string, string>();
            foreach (var (key, color) in colors)
                result[key] = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            return result;
        }
        return new();
    }
}
