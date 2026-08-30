using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Tarea.Converters;
using Tarea.Models;
using Tarea.Services;
using Tarea.ViewModels;

namespace Tarea.Views;

public partial class MainWindow : Window
{
    private readonly DataService _dataService;
    private readonly CasaViewModel _casaVm;
    private readonly RoomViewModel _roomVm;
    private readonly KeyboardShortcutService _shortcutService;

    private CasaView? _casaView;
    private RoomView? _roomView; 
    private UserControl? _settingsView;

    private string _currentPage = "Casa";
    private string _previousPage = "Casa";


    public MainWindow()
    {
        InitializeComponent();

        _dataService = new DataService();
        _dataService.Load();

        RestoreWindowGeometry();
        Closing += MainWindow_Closing;

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

        UrgencyToBrushConverter.Settings = _dataService.Settings;

        _casaVm = new CasaViewModel(_dataService);
        _casaVm.ConfirmAction = msg =>
        {
            // Simple sync confirm — RetroDialog will come later
            // For now, always confirm
            return true;
        };

        _roomVm = new RoomViewModel(_dataService);
        _shortcutService = new KeyboardShortcutService(_dataService);

        _casaView = new CasaView(_dataService);
        _casaVm.ConfirmAction = msg => true;
        _casaVm.NavigateToRoom += OnNavigateToRoom;

        _roomView = new RoomView(_dataService);
        _roomVm.ConfirmAction = msg => true;
        _roomVm.AnimateHideCard = card => _roomView.PulseAndHideCard(card);

        KeyDown += MainWindow_KeyDown;

        ApplyCrtEffects();
        RunBootSequence();
    }


    // ── Boot Sequence ──────────────────────────────────
    private static readonly string[] BootJokes = new[]
    {
        "> ̶s̶t̶e̶a̶l̶i̶n̶g̶ fetching your data...",
        "> asking ai for permission to launch...",
        "> convincing pixels to cooperate...",
        "> negotiating with the task gods...",
        "> definitely not reading your notes...",
        "> bribing the compiler...",
        "> warming up the phosphors...",
        "> reticulating splines...",
        "> caffeinating the codebase...",
    };

    private void RunBootSequence()
    {
        if (!_dataService.Settings.ShowBootSequence)
        {
            BootOverlay.IsVisible = false;
            ShowCasa();
            return;
        }

        var roomCount = _dataService.Rooms.Count;
        var cardCount = _dataService.TotalCards;
        var doneCount = _dataService.DoneCards;

        var joke = BootJokes[new Random().Next(BootJokes.Length)];

        var lines = new (TextBlock control, string text)[]
        {
            (BootLine1, "> initializing tarea..."),
            (BootLine2, joke),
            (BootLine3, $"> mounting {roomCount} room{(roomCount != 1 ? "s" : "")}..."),
            (BootLine4, $"> {cardCount} card{(cardCount != 1 ? "s" : "")} loaded ({doneCount} done)"),
            (BootLine5, "tarea"),
        };

        TypewriterSequence(lines, 0, () =>
        {
            var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            fadeTimer.Tick += (s, e) =>
            {
                fadeTimer.Stop();
                // Fade out the boot overlay
                AnimateOpacity(BootOverlay, 1, 0, 400, () =>
                {
                    BootOverlay.IsVisible = false;
                    BootOverlay.Opacity = 1; // reset
                    ShowCasa();
                });
            };
            fadeTimer.Start();
        });
    }

    private void TypewriterSequence((TextBlock control, string text)[] lines, int index, Action onComplete)
    {
        if (index >= lines.Length)
        {
            onComplete();
            return;
        }

        // Fade previous lines
        for (int i = 0; i < index; i++)
        {
            var prev = lines[i].control;
            double targetOpacity = Math.Max(0.15, 1.0 - ((index - i) * 0.3));
            AnimateOpacity(prev, prev.Opacity, targetOpacity, 200);
        }

        var (control, text) = lines[index];
        TypewriterReveal(control, text, 25, () =>
        {
            var pause = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            pause.Tick += (s, e) =>
            {
                pause.Stop();
                TypewriterSequence(lines, index + 1, onComplete);
            };
            pause.Start();
        });
    }

    private void TypewriterReveal(TextBlock target, string fullText, int charDelayMs = 25, Action? onComplete = null)
    {
        target.Text = "";
        target.IsVisible = true;

        int charIndex = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(charDelayMs) };
        timer.Tick += (s, e) =>
        {
            if (charIndex < fullText.Length)
            {
                target.Text = fullText.Substring(0, charIndex + 1) + "▌";
                charIndex++;
            }
            else
            {
                target.Text = fullText;
                timer.Stop();
                onComplete?.Invoke();
            }
        };
        timer.Start();
    }


    // ── Navigation ─────────────────────────────────────────
    private void NavigateTo(string page)
    {
        switch (page)
        {
            case "Casa":
                ShowCasa();
                break;
            case "Room":
                NavContext.Text = _roomVm.RoomTitle;
                ContentArea.Content = _roomView;
                _currentPage = "Room";
                ClearFooter();
                UpdateFooter();
                break;
            case "Settings":
                NavContext.Text = "";
                // ContentArea.Content = _settingsView;  // Phase 4
                _currentPage = "Settings";
                ClearFooter();
                UpdateFooter();
                break;
        }
    }

    private void ShowCasa()
    {
        _casaVm.Refresh();
        _casaView!.DataContext = _casaVm;
        NavContext.Text = "";
        ContentArea.Content = _casaView;
        _currentPage = "Casa";
        ClearFooter();
        UpdateFooter();
        _casaView.AnimateEntrance();
    }

    private void OnNavigateToRoom(string roomId)
    {
        _roomVm.LoadRoom(roomId);
        _roomView!.DataContext = _roomVm;
        NavContext.Text = _roomVm.RoomTitle;
        ContentArea.Content = _roomView;
        _currentPage = "Room";
        ClearFooter();
        UpdateFooter();
        _roomView.AnimateEntrance();
    }

    private void NavCasa_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowCasa();
    }

    private void NavSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _previousPage = _currentPage;
        NavigateTo("Settings");
    }

    private void UpdateFooter()
    {
        string stat1, stat2, stat3;

        if (_currentPage == "Casa")
        {
            stat1 = $"> rooms: {_dataService.Rooms.Count}";
            stat2 = $"active: {_dataService.ActiveCards}";
            stat3 = $"done: {_dataService.DoneCards}";
        }
        else if (_currentPage == "Room")
        {
            stat1 = $"> cards: {_roomVm.Cards.Count}";
            stat2 = _roomVm.ProgressSummary;
            stat3 = "";
        }
        else // Settings
        {
            var s = _dataService.Settings;
            stat1 = $"> theme: {s.Theme.ToString().ToLower()}";

            // Count non-default settings
            var defaults = new AppSettings();
            int changed = 0;
            if (s.CardWidth != defaults.CardWidth || s.CardHeight != defaults.CardHeight) changed++;
            if (s.FontSize != defaults.FontSize) changed++;
            if (!s.ConfirmOnDelete) changed++;
            if (s.ConfirmOnCardDelete) changed++;
            if (s.HideOnComplete) changed++;
            if (s.DefaultCardStatus != defaults.DefaultCardStatus) changed++;
            if (s.DueDateWarningDays != defaults.DueDateWarningDays) changed++;

            stat2 = changed > 0 ? $"{changed} setting{(changed != 1 ? "s" : "")} changed" : "defaults";
            stat3 = $"cards: {_dataService.TotalCards}";
        }

        if (_dataService.Settings.AnimationsEnabled && _dataService.Settings.AnimFooterTypewriter)
        {
            // Typewriter effect for footer stats
            StatTotal.Text = "";
            StatActive.Text = "";
            StatDone.Text = "";

            TypewriterReveal(StatTotal, stat1, 15, () =>
            {
                TypewriterReveal(StatActive, stat2, 15, () =>
                {
                    if (!string.IsNullOrEmpty(stat3))
                        TypewriterReveal(StatDone, stat3, 15);
                });
            });
        }
        else
        {
            StatTotal.Text = stat1;
            StatActive.Text = stat2;
            StatDone.Text = stat3;
        }
    }

    private void ClearFooter()
    {
        StatTotal.Text = "";
        StatActive.Text = "";
        StatDone.Text = "";
    }


    // ── Theme ──────────────────────────────────────────────
    public void ApplyTheme(ThemePreset theme)
    {
        _dataService.Settings.Theme = theme;
        _dataService.Save();
        ThemeService.Apply(theme);
    }

    public void ApplySavedTheme(SavedTheme saved)
    {
        _dataService.Settings.Theme = ThemePreset.Custom;
        _dataService.Settings.ActiveSavedThemeName = saved.Name;

        foreach (var (key, hex) in saved.Colors)
            _dataService.Settings.CustomThemeColors[key] = hex;

        _dataService.Save();
        ThemeService.ApplySaved(saved);
    }


    // ── Keyboard Shortcuts ─────────────────────────────────
    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_shortcutService.IsEnabled)
            return;

        // Don't intercept when typing in a TextBox
        if (FocusManager?.GetFocusedElement() is TextBox)
            return;

        var settings = _dataService.Settings;

        // --- Escape / Back ---
        if (e.Key == _shortcutService.GetKey(settings.ShortcutBack))
        {
            if (_currentPage == "Settings")
                NavigateTo(_previousPage);
            else if (_currentPage == "Room")
                NavigateTo("Casa");

            e.Handled = true;
            return;
        }

        // --- Number keys: jump to room by position (Casa only) ---
        if (_currentPage == "Casa" && e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            int roomIndex = e.Key - Key.D1;
            if (_casaVm.Rooms.Count > roomIndex)
            {
                var room = _casaVm.Rooms[roomIndex];
                OnNavigateToRoom(room.Id);
            }
            e.Handled = true;
            return;
        }

        // --- Quick Add ---
        if (e.Key == _shortcutService.GetKey(settings.ShortcutQuickAdd))
        {
            if (_currentPage == "Casa")
                _casaView?.StartQuickAdd();
            else if (_currentPage == "Room")
                _roomView?.StartQuickAdd();
            e.Handled = true;
            return;
        }

        // --- Settings ---
        if (e.Key == _shortcutService.GetKey(settings.ShortcutSettings))
        {
            if (_currentPage != "Settings")
            {
                _previousPage = _currentPage;
                NavigateTo("Settings");
            }
            e.Handled = true;
            return;
        }
    }


    // ── Title Bar Controls ─────────────────────────────────
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Allow dragging the window from the title bar area
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void TitleBar_Minimize(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void TitleBar_MaximizeRestore(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void TitleBar_Close(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }


    // ── Animation / Effects ─────────────────────────────────
    public void ApplyCrtEffects()
    {
        var s = _dataService.Settings;
        bool master = s.AnimationsEnabled;

        // Scanlines
        ScanlineOverlay.Children.Clear();
        if (master && s.AnimScanlines)
        {
            ScanlineOverlay.IsVisible = true;
            for (double y = 0; y < 2000; y += 4)
            {
                var line = new Line
                {
                    StartPoint = new Point(0, y),
                    EndPoint = new Point(4000, y),
                    Stroke = Brushes.White,
                    StrokeThickness = 0.9,
                };
                ScanlineOverlay.Children.Add(line);
            }
        }
        else
        {
            ScanlineOverlay.IsVisible = false;
        }

        // Vignette
        VignetteOverlay.IsVisible = master && s.AnimVignette;
    }

    /// <summary>
    /// Simple opacity animation using DispatcherTimer (works cross-platform).
    /// Replaces WPF's BeginAnimation(OpacityProperty, ...).
    /// </summary>
    private void AnimateOpacity(Control target, double from, double to, int durationMs, Action? onComplete = null)
    {
        target.Opacity = from;
        int steps = Math.Max(1, durationMs / 16); // ~60fps
        double delta = (to - from) / steps;
        int currentStep = 0;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (s, e) =>
        {
            currentStep++;
            if (currentStep >= steps)
            {
                timer.Stop();
                target.Opacity = to;
                onComplete?.Invoke();
            }
            else
            {
                target.Opacity = from + (delta * currentStep);
            }
        };
        timer.Start();
    }


    // ── Window Geometry ──────────────────────────────────────
    private void RestoreWindowGeometry()
    {
        var s = _dataService.Settings;

        Width = s.WindowWidth;
        Height = s.WindowHeight;

        if (s.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        if (s.WindowLeft >= 0 && s.WindowTop >= 0)
        {
            Position = new PixelPoint((int)s.WindowLeft, (int)s.WindowTop);
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        var s = _dataService.Settings;
        s.IsMaximized = WindowState == WindowState.Maximized;

        // Save the restored (non-maximized) bounds
        if (WindowState == WindowState.Normal)
        {
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            s.WindowLeft = Position.X;
            s.WindowTop = Position.Y;
        }

        _dataService.Save();
    }
}