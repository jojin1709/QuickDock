using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Settings;

namespace QuickDock.Services.System;

public sealed class ShortcutManagerService : IShortcutManagerService
{
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<ShortcutManagerService> _logger;

    public ShortcutManagerService(ISettingsManager settingsManager, ILogger<ShortcutManagerService> logger)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<ShortcutItem> GetShortcuts()
    {
        var s = _settingsManager.CurrentSettings.Shortcuts;
        return
        [
            new ShortcutItem("dock.toggle", "Open QuickDock", "Toggle QuickDock expand/collapse from anywhere", s.OpenDock, "Ctrl+Space", true),
            new ShortcutItem("timer.new", "New Timer", "Quickly create and start a new countdown timer", s.NewTimer, "Ctrl+Shift+T", false),
            new ShortcutItem("clipboard.open", "Open Clipboard", "Surface clipboard history directly", s.OpenClipboard, "Ctrl+Shift+V", false),
            new ShortcutItem("notes.open", "Open Notes", "Open quick scratchpad for drafting notes", s.OpenNotes, "Ctrl+Shift+N", false),
            new ShortcutItem("dev.open", "Open Developer Toolkit", "Jump directly to developer tools and utilities", s.OpenDevToolkit, "Ctrl+Shift+D", false),
            new ShortcutItem("security.open", "Open Network & Security", "Jump directly to network and security diagnostics", s.OpenNetworkSecurity, "Ctrl+Shift+S", false)
        ];
    }

    public ShortcutUpdateResult UpdateShortcut(string shortcutId, string newKeyGesture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutId);

        if (string.IsNullOrWhiteSpace(newKeyGesture))
        {
            return new ShortcutUpdateResult(false, "Shortcut key combination cannot be empty.");
        }

        string normalized = NormalizeKeyGesture(newKeyGesture);
        if (!IsValidKeyGesture(normalized))
        {
            return new ShortcutUpdateResult(false, "Invalid shortcut format. Must include at least one modifier key (Ctrl, Shift, or Alt) and a valid key.");
        }

        var existing = GetShortcuts();
        var currentItem = existing.FirstOrDefault(x => string.Equals(x.Id, shortcutId, StringComparison.OrdinalIgnoreCase));
        if (currentItem == null)
        {
            return new ShortcutUpdateResult(false, $"Unknown shortcut ID '{shortcutId}'.");
        }

        // Detect conflicts with other shortcuts
        var conflict = existing.FirstOrDefault(x =>
            !string.Equals(x.Id, shortcutId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.CurrentKeyGesture, normalized, StringComparison.OrdinalIgnoreCase));

        if (conflict != null)
        {
            return new ShortcutUpdateResult(false, $"Conflict detected: '{normalized}' is already assigned to '{conflict.Name}'.");
        }

        var settings = _settingsManager.CurrentSettings;
        switch (shortcutId.ToLowerInvariant())
        {
            case "dock.toggle":
                settings.Shortcuts.OpenDock = normalized;
                settings.Behavior.GlobalHotkey = normalized;
                break;
            case "timer.new":
                settings.Shortcuts.NewTimer = normalized;
                break;
            case "clipboard.open":
                settings.Shortcuts.OpenClipboard = normalized;
                break;
            case "notes.open":
                settings.Shortcuts.OpenNotes = normalized;
                break;
            case "dev.open":
                settings.Shortcuts.OpenDevToolkit = normalized;
                break;
            case "security.open":
                settings.Shortcuts.OpenNetworkSecurity = normalized;
                break;
            default:
                return new ShortcutUpdateResult(false, $"Unsupported shortcut ID: {shortcutId}");
        }

        _ = _settingsManager.SaveAsync(settings, "Shortcuts");
        _logger.LogInformation("Updated shortcut {Id} to {Gesture}", shortcutId, normalized);
        return new ShortcutUpdateResult(true);
    }

    public void ResetToDefaults()
    {
        var settings = _settingsManager.CurrentSettings;
        var defaults = new ShortcutSettings();
        settings.Shortcuts = defaults;
        settings.Behavior.GlobalHotkey = defaults.OpenDock;
        _ = _settingsManager.SaveAsync(settings, "Shortcuts");
        _logger.LogInformation("All keyboard shortcuts reset to default keybindings.");
    }

    private static string NormalizeKeyGesture(string input)
    {
        var parts = input.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var modifiers = new List<string>();
        string key = string.Empty;

        foreach (var p in parts)
        {
            if (p.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("control", StringComparison.OrdinalIgnoreCase))
                modifiers.Add("Ctrl");
            else if (p.Equals("shift", StringComparison.OrdinalIgnoreCase))
                modifiers.Add("Shift");
            else if (p.Equals("alt", StringComparison.OrdinalIgnoreCase))
                modifiers.Add("Alt");
            else if (p.Equals("win", StringComparison.OrdinalIgnoreCase) || p.Equals("windows", StringComparison.OrdinalIgnoreCase))
                modifiers.Add("Win");
            else
                key = p.ToUpperInvariant();
        }

        modifiers.Sort();
        if (!string.IsNullOrEmpty(key))
        {
            modifiers.Add(key);
        }

        return string.Join("+", modifiers);
    }

    private static bool IsValidKeyGesture(string gesture)
    {
        var parts = gesture.Split('+');
        if (parts.Length < 2) return false;
        bool hasModifier = parts.Any(p => p is "Ctrl" or "Shift" or "Alt" or "Win");
        return hasModifier && !string.IsNullOrWhiteSpace(parts[^1]);
    }
}

