using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using Tarea.Models;

namespace Tarea.Services;

public class KeyboardShortcutService
{
    private readonly DataService _dataService;

    public KeyboardShortcutService(DataService dataService)
    {
        _dataService = dataService;
    }

    public bool IsEnabled => _dataService.Settings.KeyboardShortcutsEnabled;

    public void SetEnabled(bool enabled)
    {
        _dataService.Settings.KeyboardShortcutsEnabled = enabled;
        _dataService.Save();
    }

    public Key GetKey(string settingsValue)
    {
        if (Enum.TryParse<Key>(settingsValue, out var key))
            return key;

        return Key.None;
    }

    public string KeyToDisplay(string settingValue)
    {
        if (Enum.TryParse<Key>(settingValue, out var key))
        {
            return key switch
            {
                Key.Escape => "Esc",
                Key.Oem2 => "/",
                _ => key.ToString()
            };
        }

        return settingValue;
    }

    public bool TrySetShortcut(string property, Key newKey)
    {
        var keyString = newKey.ToString();

        // Block keys that would conflict with text input
        if (newKey is Key.Space or Key.Back or Key.Delete or Key.Tab or Key.Enter)
            return false;

        // Check for duplicates across all shortcuts
        var settings = _dataService.Settings;
        var allShortcuts = new Dictionary<string, string>
        {
            [nameof(settings.ShortcutBack)] = settings.ShortcutBack,
            [nameof(settings.ShortcutQuickAdd)] = settings.ShortcutQuickAdd,
            [nameof(settings.ShortcutSettings)] = settings.ShortcutSettings,
        };

        // Remove the one we're changing so it doesn't conflict with itself
        allShortcuts.Remove(property);

        if (allShortcuts.Values.Contains(keyString, StringComparer.OrdinalIgnoreCase))
            return false;

        // Apply
        switch (property)
        {
            case nameof(settings.ShortcutBack):
                settings.ShortcutBack = keyString;
                break;
            case nameof(settings.ShortcutQuickAdd):
                settings.ShortcutQuickAdd = keyString;
                break;
            case nameof(settings.ShortcutSettings):
                settings.ShortcutSettings = keyString;
                break;
            default:
                return false;
        }

        _dataService.Save();
        return true;
    }
}
