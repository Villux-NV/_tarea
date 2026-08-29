using System.Linq;
using Avalonia.Controls;
using Tarea.Models;
using Tarea.Services;

namespace Tarea.Views;

public partial class MainWindow : Window
{
    private readonly DataService _dataService;

    public MainWindow()
    {
        InitializeComponent();

        _dataService = new DataService();
        _dataService.Load();

        // Apply saved theme on startup
        var settings = _dataService.Settings;
        if (settings.Theme == ThemePreset.Custom
            && !string.IsNullOrEmpty(settings.ActiveSavedThemeName))
        {
            var saved = settings.SavedThemes
                .FirstOrDefault(t => t.Name == settings.ActiveSavedThemeName);
            if (saved != null)
                ThemeService.ApplySaved(saved);
            else
                ThemeService.Apply(ThemePreset.Custom, settings);
        }
        else
        {
            ThemeService.Apply(settings.Theme, settings);
        }

        // Show stats to verify data layer is working
        var roomCount = _dataService.Rooms.Count;
        var cardCount = _dataService.TotalCards;
        var doneCount = _dataService.DoneCards;

        StatsText.Text = $"> {roomCount} room{(roomCount != 1 ? "s" : "")} · "
                       + $"{cardCount} card{(cardCount != 1 ? "s" : "")} · "
                       + $"{doneCount} done";
    }
}
