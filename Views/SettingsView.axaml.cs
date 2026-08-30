using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Tarea.Helpers;
using Tarea.Models;
using Tarea.Services;
using static System.Net.Mime.MediaTypeNames;

namespace Tarea.Views;

public partial class SettingsView : UserControl
{
    private readonly DataService _dataService;
    private Button? _activeRebindButton;

    private static readonly (int W, int H) SizeCompactValues = (220, 280);
    private static readonly (int W, int H) SizeDefaultValues = (256, 320);
    private static readonly (int W, int H) SizeLargeValues = (300, 380);

    public SettingsView() : this(null!) { }

    public SettingsView(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;

        var dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tarea");
        TxtDataPath.Text = $"projects.json stored at: {dataFolder}";

        Refresh();
    }

    public void Refresh()
    {
        var settings = _dataService.Settings;

        // behavior checkboxes
        ChkConfirmDelete.IsChecked = settings.ConfirmOnDelete;
        ChkConfirmCardDelete.IsChecked = settings.ConfirmOnCardDelete;

        // theme highlight
        HighlightSelected(ThemePanel, settings.Theme.ToString());

        // saved themes list
        RefreshSavedThemes();

        // custom editor visibility
        CustomThemeEditor.IsVisible = settings.Theme == ThemePreset.Custom;

        if (settings.Theme == ThemePreset.Custom)
            RefreshCustomEditor();

        // card size highlight + custom panel visibility
        var isCustomSize = settings.CardWidth != SizeCompactValues.W
                        && settings.CardWidth != SizeDefaultValues.W
                        && settings.CardWidth != SizeLargeValues.W;
        var sizeTag = isCustomSize ? "Custom"
                    : settings.CardWidth == SizeCompactValues.W ? "Compact"
                    : settings.CardWidth == SizeLargeValues.W ? "Large"
                    : "Default";
        HighlightSelected(CardSizePanel, sizeTag);
        CustomSizePanel.IsVisible = isCustomSize;
        TxtCardWidth.Text = settings.CardWidth.ToString();
        TxtCardHeight.Text = settings.CardHeight.ToString();

        // font size highlight
        HighlightSelected(FontSizePanel, settings.FontSize.ToString());

        // default status highlight
        HighlightSelected(DefaultStatusPanel, settings.DefaultCardStatus.ToString());

        // due date warning highlight
        HighlightSelected(DueDatePanel, settings.DueDateWarningDays.ToString());

        // status labels
        TxtTodoLabel.Text = settings.TodoLabel;
        TxtWipLabel.Text = settings.WipLabel;
        TxtDoneLabel.Text = settings.DoneLabel;

        // keyboard shortcuts
        var shortcutService = new KeyboardShortcutService(_dataService);
        ShortcutsToggle.IsChecked = settings.KeyboardShortcutsEnabled;

        DisplayBack.Text = shortcutService.KeyToDisplay(settings.ShortcutBack);
        DisplayQuickAdd.Text = shortcutService.KeyToDisplay(settings.ShortcutQuickAdd);
        DisplaySettings.Text = shortcutService.KeyToDisplay(settings.ShortcutSettings);

        BindBack.Content = shortcutService.KeyToDisplay(settings.ShortcutBack);
        BindQuickAdd.Content = shortcutService.KeyToDisplay(settings.ShortcutQuickAdd);
        BindSettings.Content = shortcutService.KeyToDisplay(settings.ShortcutSettings);

        // hide on complete
        ChkHideOnComplete.IsChecked = settings.HideOnComplete;
        HideDelayPanel.IsVisible = settings.HideOnComplete;
        HighlightSelected(HideDelayOptions, settings.HideOnCompleteDelay.ToString());

        // Animation toggles
        ChkAnimMaster.IsChecked = settings.AnimationsEnabled;
        ChkAnimScanlines.IsChecked = settings.AnimScanlines;
        ChkAnimVignette.IsChecked = settings.AnimVignette;
        ChkAnimHoverScale.IsChecked = settings.AnimCardHoverScale;
        ChkAnimDonePulse.IsChecked = settings.AnimDonePulse;
        ChkAnimSmoothHide.IsChecked = settings.AnimSmoothHide;
        ChkAnimTypewriter.IsChecked = settings.AnimFooterTypewriter;
        AnimTogglesPanel.Opacity = settings.AnimationsEnabled ? 1.0 : 0.4;
        AnimTogglesPanel.IsEnabled = settings.AnimationsEnabled;

        // boot sequence
        ChkBootSequence.IsChecked = settings.ShowBootSequence;

        RefreshUrgencyPreviews();
    }


    // ── Boot Sequence ──────────────────────────────────────
    private void BootSequence_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dataService.Settings.ShowBootSequence = ChkBootSequence.IsChecked ?? true;
        _dataService.Save();
    }


    // ── Appearance ──────────────────────────────────────────
    private void ThemePreset_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag
            && Enum.TryParse<ThemePreset>(tag, out var preset))
        {
            var mainWindow = this.FindAncestorOfType<MainWindow>();
            mainWindow?.ApplyTheme(preset);

            HighlightSelected(ThemePanel, tag);
            CustomThemeEditor.IsVisible = preset == ThemePreset.Custom;

            if (preset == ThemePreset.Custom)
                RefreshCustomEditor();
        }
    }

    private void CardSize_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag)
        {
            if (tag == "Custom")
            {
                CustomSizePanel.IsVisible = true;
                TxtCardWidth.Text = _dataService.Settings.CardWidth.ToString();
                TxtCardHeight.Text = _dataService.Settings.CardHeight.ToString();
                HighlightSelected(CardSizePanel, "Custom");
                return;
            }

            var (w, h) = tag switch
            {
                "Compact" => SizeCompactValues,
                "Large" => SizeLargeValues,
                _ => SizeDefaultValues
            };

            _dataService.Settings.CardWidth = w;
            _dataService.Settings.CardHeight = h;
            _dataService.Save();

            HighlightSelected(CardSizePanel, tag);
            CustomSizePanel.IsVisible = false;
        }
    }

    private void CustomCardSize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (int.TryParse(TxtCardWidth.Text?.Trim(), out var w) && w >= 140 && w <= 500
            && int.TryParse(TxtCardHeight.Text?.Trim(), out var h) && h >= 180 && h <= 600)
        {
            _dataService.Settings.CardWidth = w;
            _dataService.Settings.CardHeight = h;
            _dataService.Save();
            HighlightSelected(CardSizePanel, "");
            TxtCardWidth.Text = w.ToString();
            TxtCardHeight.Text = h.ToString();
        }
    }

    private void FontSize_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag
            && int.TryParse(tag, out var size))
        {
            _dataService.Settings.FontSize = size;
            _dataService.Save();
            ThemeService.ApplyFontSize(size);
            HighlightSelected(FontSizePanel, tag);
        }
    }


    // ── Behavior ────────────────────────────────────────────
    private void ConfirmDelete_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dataService.Settings.ConfirmOnDelete = ChkConfirmDelete.IsChecked ?? true;
        _dataService.Save();
    }

    private void ConfirmCardDelete_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dataService.Settings.ConfirmOnCardDelete = ChkConfirmCardDelete.IsChecked ?? false;
        _dataService.Save();
    }

    private void DefaultStatus_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag
            && Enum.TryParse<CardStatus>(tag, out var status))
        {
            _dataService.Settings.DefaultCardStatus = status;
            _dataService.Save();
            HighlightSelected(DefaultStatusPanel, tag);
        }
    }

    private void DueDateWarning_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag
            && int.TryParse(tag, out var days))
        {
            _dataService.Settings.DueDateWarningDays = days;
            _dataService.Save();
            HighlightSelected(DueDatePanel, tag);
        }
    }

    private void ApplyStatusLabels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var todo = TxtTodoLabel.Text?.Trim();
        var wip = TxtWipLabel.Text?.Trim();
        var done = TxtDoneLabel.Text?.Trim();

        if (!string.IsNullOrEmpty(todo)) _dataService.Settings.TodoLabel = todo;
        if (!string.IsNullOrEmpty(wip)) _dataService.Settings.WipLabel = wip;
        if (!string.IsNullOrEmpty(done)) _dataService.Settings.DoneLabel = done;

        _dataService.Save();
    }

    private void ResetStatusLabels_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dataService.Settings.TodoLabel = "TODO";
        _dataService.Settings.WipLabel = "WIP";
        _dataService.Settings.DoneLabel = "DONE";
        _dataService.Save();

        TxtTodoLabel.Text = "TODO";
        TxtWipLabel.Text = "WIP";
        TxtDoneLabel.Text = "DONE";
    }

    private void HideOnComplete_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dataService.Settings.HideOnComplete = ChkHideOnComplete.IsChecked ?? false;
        _dataService.Save();
        HideDelayPanel.IsVisible = _dataService.Settings.HideOnComplete;
    }

    private void HideDelay_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is string tag
            && int.TryParse(tag, out var seconds))
        {
            _dataService.Settings.HideOnCompleteDelay = seconds;
            _dataService.Save();
            HighlightSelected(HideDelayOptions, tag);
        }
    }


    // ── Urgency ─────────────────────────────────────────────
    private void RefreshUrgencyPreviews()
    {
        var settings = _dataService.Settings;
        TxtUrgencyLow.Text = settings.UrgencyLowColor;
        TxtUrgencyMed.Text = settings.UrgencyMediumColor;
        TxtUrgencyHigh.Text = settings.UrgencyHighColor;

        TrySetPreviewBackground(UrgencyLowPreview, settings.UrgencyLowColor);
        TrySetPreviewBackground(UrgencyMedPreview, settings.UrgencyMediumColor);
        TrySetPreviewBackground(UrgencyHighPreview, settings.UrgencyHighColor);
    }

    private static void TrySetPreviewBackground(Border preview, string hex)
    {
        try
        {
            var color = Color.Parse(hex);
            preview.Background = new SolidColorBrush(color);
        }
        catch
        {
            preview.Background = Brushes.Transparent;
        }
    }

    private void ApplyUrgencyColors_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var low = TxtUrgencyLow.Text?.Trim() ?? "";
        var med = TxtUrgencyMed.Text?.Trim() ?? "";
        var high = TxtUrgencyHigh.Text?.Trim() ?? "";

        if (TryParseHex(low)) _dataService.Settings.UrgencyLowColor = low;
        if (TryParseHex(med)) _dataService.Settings.UrgencyMediumColor = med;
        if (TryParseHex(high)) _dataService.Settings.UrgencyHighColor = high;

        _dataService.Save();
        RefreshUrgencyPreviews();
    }

    private void ResetUrgencyColors_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dataService.Settings.UrgencyLowColor = "#18FBA824";
        _dataService.Settings.UrgencyMediumColor = "#22FC5B13";
        _dataService.Settings.UrgencyHighColor = "#2AEF4444";
        _dataService.Save();
        RefreshUrgencyPreviews();
    }

    private void ToggleUrgencyEditor_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UrgencyEditorPanel.IsVisible = !UrgencyEditorPanel.IsVisible;
    }

    private void ToggleStatusLabelEditor_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        StatusLabelEditorPanel.IsVisible = !StatusLabelEditorPanel.IsVisible;
    }


    // ── Custom Theme ────────────────────────────────────────
    private void RefreshCustomEditor()
    {
        var colors = _dataService.Settings.CustomThemeColors;
        var items = ThemeService.PaletteKeys.Select(key => new CustomColorItem
        {
            Key = key,
            Label = ThemeService.PaletteLabels.TryGetValue(key, out var label) ? label : key,
            Hex = colors.TryGetValue(key, out var hex) ? hex : "#FF00FF"
        }).ToList();

        CustomColorList.ItemsSource = null;
        CustomColorList.ItemsSource = items;
    }

    private void CopyPresetToCustom_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag
            && Enum.TryParse<ThemePreset>(tag, out var preset))
        {
            var presetColors = ThemeService.GetPresetColors(preset);
            foreach (var (key, hex) in presetColors)
                _dataService.Settings.CustomThemeColors[key] = hex;

            _dataService.Save();
            RefreshCustomEditor();

            var mainWindow = this.FindAncestorOfType<MainWindow>();
            mainWindow?.ApplyTheme(ThemePreset.Custom);
        }
    }

    private void ApplyCustomTheme_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Walk visual tree to read TextBox values directly
        var panel = ViewHelpers.FindVisualChild<Panel>(CustomColorList);
        if (panel == null) return;

        int index = 0;
        foreach (var container in panel.GetVisualChildren())
        {
            if (index >= CustomColorList.Items.Count) break;

            var item = CustomColorList.Items.Cast<CustomColorItem>().ElementAtOrDefault(index);
            var textBox = ViewHelpers.FindVisualChild<TextBox>(container as Visual ?? container as Visual);

            if (item != null && textBox != null)
            {
                var hex = textBox.Text?.Trim() ?? "";
                if (hex.StartsWith("#") && hex.Length >= 4 && TryParseHex(hex))
                    _dataService.Settings.CustomThemeColors[item.Key] = hex;
            }

            index++;
        }

        _dataService.Settings.Theme = ThemePreset.Custom;
        _dataService.Save();
        ThemeService.Apply(ThemePreset.Custom, _dataService.Settings);
        RefreshCustomEditor();
    }

    private void SaveCustomPreset_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var name = TxtSaveThemeName.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) return;

        var colors = new Dictionary<string, string>();
        var panel = ViewHelpers.FindVisualChild<Panel>(CustomColorList);
        if (panel == null) return;

        int index = 0;
        foreach (var container in panel.GetVisualChildren())
        {
            if (index >= CustomColorList.Items.Count) break;

            var item = CustomColorList.Items.Cast<CustomColorItem>().ElementAtOrDefault(index);
            var textBox = ViewHelpers.FindVisualChild<TextBox>(container as Visual ?? container as Visual);

            if (item != null && textBox != null)
            {
                var hex = textBox.Text?.Trim() ?? "";
                if (hex.StartsWith("#") && hex.Length >= 4 && TryParseHex(hex))
                    colors[item.Key] = hex;
            }

            index++;
        }

        if (colors.Count == 0) return;

        _dataService.Settings.SavedThemes.RemoveAll(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        _dataService.Settings.SavedThemes.Add(new SavedTheme
        {
            Name = name,
            Colors = colors
        });

        _dataService.Save();
        TxtSaveThemeName.Text = "";
        RefreshSavedThemes();
    }

    private void SavedTheme_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is SavedThemeItem item)
        {
            var saved = _dataService.Settings.SavedThemes
                .FirstOrDefault(t => t.Name == item.Name);

            if (saved != null)
            {
                var mainWindow = this.FindAncestorOfType<MainWindow>();
                mainWindow?.ApplySavedTheme(saved);

                HighlightSelected(ThemePanel, "Custom");
                CustomThemeEditor.IsVisible = true;
                RefreshCustomEditor();
            }
        }
    }

    private void DeleteSavedTheme_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
        {
            _dataService.Settings.SavedThemes.RemoveAll(t => t.Name == name);
            if (_dataService.Settings.ActiveSavedThemeName == name)
                _dataService.Settings.ActiveSavedThemeName = "";

            _dataService.Save();
            RefreshSavedThemes();
        }
    }


    // ── Shortcuts ────────────────────────────────────────────
    private void ShortcutsToggle_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _dataService.Settings.KeyboardShortcutsEnabled = ShortcutsToggle.IsChecked ?? true;
        _dataService.Save();
    }

    private void RebindShortcut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            _activeRebindButton = btn;
            btn.Content = "...";
            btn.Focus();
        }
    }

    private void RebindShortcut_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_activeRebindButton == null) return;
        if (sender != _activeRebindButton) return;

        var property = _activeRebindButton.Tag as string;
        if (property == null) return;

        var shortcutService = new KeyboardShortcutService(_dataService);
        bool success = shortcutService.TrySetShortcut(property, e.Key);

        if (success)
        {
            _activeRebindButton.Content = shortcutService.KeyToDisplay(e.Key.ToString());
            DisplayBack.Text = shortcutService.KeyToDisplay(_dataService.Settings.ShortcutBack);
            DisplayQuickAdd.Text = shortcutService.KeyToDisplay(_dataService.Settings.ShortcutQuickAdd);
            DisplaySettings.Text = shortcutService.KeyToDisplay(_dataService.Settings.ShortcutSettings);
        }
        else
        {
            var originalBrush = _activeRebindButton.BorderBrush;
            _activeRebindButton.BorderBrush = new SolidColorBrush(Colors.Red);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            var btn = _activeRebindButton;
            timer.Tick += (s, _) =>
            {
                btn.BorderBrush = originalBrush;
                timer.Stop();
            };
            timer.Start();
        }

        _activeRebindButton = null;
        e.Handled = true;
    }

    private void ToggleShortcutEditor_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShortcutEditorPanel.IsVisible = !ShortcutEditorPanel.IsVisible;
    }


    // ── Animations ──────────────────────────────────────────
    private void AnimMaster_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var enabled = ChkAnimMaster.IsChecked ?? true;
        _dataService.Settings.AnimationsEnabled = enabled;
        _dataService.Save();

        AnimTogglesPanel.Opacity = enabled ? 1.0 : 0.4;
        AnimTogglesPanel.IsEnabled = enabled;

        var mainWindow = this.FindAncestorOfType<MainWindow>();
        mainWindow?.ApplyCrtEffects();
    }

    private void AnimToggle_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var s = _dataService.Settings;
        s.AnimScanlines = ChkAnimScanlines.IsChecked ?? true;
        s.AnimVignette = ChkAnimVignette.IsChecked ?? true;
        s.AnimCardHoverScale = ChkAnimHoverScale.IsChecked ?? true;
        s.AnimDonePulse = ChkAnimDonePulse.IsChecked ?? true;
        s.AnimSmoothHide = ChkAnimSmoothHide.IsChecked ?? true;
        s.AnimFooterTypewriter = ChkAnimTypewriter.IsChecked ?? true;
        _dataService.Save();

        var mainWindow = this.FindAncestorOfType<MainWindow>();
        mainWindow?.ApplyCrtEffects();
    }


    // ── Data ────────────────────────────────────────────────
    private void OpenDataFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tarea");

        if (Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }

    private async void ExportMarkdown_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var markdown = ExportService.ExportAllToMarkdown(_dataService);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export all rooms",
            SuggestedFileName = "tarea-export",
            DefaultExtension = "md",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Markdown") { Patterns = new[] { "*.md" } },
                new FilePickerFileType("Text") { Patterns = new[] { "*.txt" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*.*" } },
            }
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(markdown);
        }
    }


    // ── Helpers ─────────────────────────────────────────────
    private void HighlightSelected(Panel parent, string selectedTag)
    {
        ISolidColorBrush roseBrush = Brushes.White;
        ISolidColorBrush mutedBrush = Brushes.Gray;

        if (Avalonia.Application.Current!.Resources.TryGetResource("RoseBrush", null, out var r)
            && r is ISolidColorBrush rb)
            roseBrush = rb;
        if (Avalonia.Application.Current!.Resources.TryGetResource("MutedBrush", null, out var m)
            && m is ISolidColorBrush mb)
            mutedBrush = mb;

        foreach (var child in parent.Children)
        {
            if (child is Border b)
            {
                var isSelected = (b.Tag as string) == selectedTag;
                b.BorderBrush = isSelected ? roseBrush : mutedBrush;
            }
        }
    }

    private static bool TryParseHex(string hex)
    {
        if (string.IsNullOrEmpty(hex) || !hex.StartsWith("#"))
            return false;
        try
        {
            Color.Parse(hex);
            return true;
        }
        catch { return false; }
    }

    private void RefreshSavedThemes()
    {
        var items = _dataService.Settings.SavedThemes.Select(t => new SavedThemeItem
        {
            Name = t.Name,
            BackgroundHex = t.Colors.TryGetValue("Black", out var bg) ? bg : "#1A1A1A",
            ForegroundHex = t.Colors.TryGetValue("Rose", out var fg) ? fg : "#AAAAAA",
            MutedHex = t.Colors.TryGetValue("Muted", out var mu) ? mu : "#666666",
            Colors = t.Colors
        }).ToList();

        SavedThemesList.ItemsSource = null;
        SavedThemesList.ItemsSource = items;
    }
}

public class CustomColorItem
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
}

public class SavedThemeItem
{
    public string Name { get; set; } = string.Empty;
    public string BackgroundHex { get; set; } = "#1A1A1A";
    public string ForegroundHex { get; set; } = "#AAAAAA";
    public string MutedHex { get; set; } = "#666666";
    public Dictionary<string, string> Colors { get; set; } = new();
}
