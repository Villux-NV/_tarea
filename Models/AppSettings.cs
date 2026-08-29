using System.Collections.Generic;

namespace Tarea.Models;

public enum ThemePreset
{
    Rose,
    Amber,
    Integrale,
    Custom
}

public class SavedTheme
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Colors { get; set; } = new();
}

public class AppSettings
{
    // ── Appearance ─────────────────────────────────────────
    public ThemePreset Theme { get; set; } = ThemePreset.Rose;
    public List<SavedTheme> SavedThemes { get; set; } = new();
    public string ActiveSavedThemeName { get; set; } = string.Empty;
    public int CardWidth { get; set; } = 256;
    public int CardHeight { get; set; } = 320;
    public int FontSize { get; set; } = 14;

    // ── Status Labels ────────────────────────────────────────
    public string TodoLabel { get; set; } = "TODO";
    public string WipLabel { get; set; } = "WIP";
    public string DoneLabel { get; set; } = "DONE";

    // ── Keyboard Shortcuts ───────────────────────────────────
    public bool KeyboardShortcutsEnabled { get; set; } = true;
    public string ShortcutBack { get; set; } = "Escape";
    public string ShortcutQuickAdd { get; set; } = "N";
    public string ShortcutSettings { get; set; } = "S";

    // ── Completion Behavior ──────────────────────────────────
    public bool HideOnComplete { get; set; } = false;
    public int HideOnCompleteDelay { get; set; } = 2;       // seconds, 0 = immediate

    // ── Retro Effects ────────────────────────────────────────
    public bool ShowBootSequence { get; set; } = true;

    // ── Animations ───────────────────────────────────────────
    public bool AnimationsEnabled { get; set; } = true;      // master toggle
    public bool AnimScanlines { get; set; } = true;
    public bool AnimVignette { get; set; } = true;
    public bool AnimCardHoverScale { get; set; } = true;
    public bool AnimDonePulse { get; set; } = true;
    public bool AnimSmoothHide { get; set; } = true;
    public bool AnimFooterTypewriter { get; set; } = true;

    // ── Window Geometry ──────────────────────────────────────
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 700;
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public bool IsMaximized { get; set; } = false;

    // ── Custom Theme Colors ──────────────────────────────────
    public Dictionary<string, string> CustomThemeColors { get; set; } = new()
    {
        ["Black"] = "#0C0002",
        ["Slate"] = "#0F172A",
        ["Rose"] = "#E8B4B8",
        ["Muted"] = "#665E5F",
        ["Green"] = "#7B832D",
        ["Orange"] = "#FC5B13",
        ["Yellow"] = "#FBA824",
        ["Blue"] = "#00B3B0",
    };

    // ── Urgency Colors ───────────────────────────────────────
    public string UrgencyLowColor { get; set; } = "#18FBA824";
    public string UrgencyMediumColor { get; set; } = "#22FC5B13";
    public string UrgencyHighColor { get; set; } = "#2AEF4444";

    // ── Behavior ─────────────────────────────────────────────
    public bool ConfirmOnDelete { get; set; } = true;
    public bool ConfirmOnCardDelete { get; set; } = false;
    public CardStatus DefaultCardStatus { get; set; } = CardStatus.Todo;
    public int DueDateWarningDays { get; set; } = 3;
}
