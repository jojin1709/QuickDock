namespace QuickDock.Core.Interfaces;

public sealed record ShortcutItem(
    string Id,
    string Name,
    string Description,
    string CurrentKeyGesture,
    string DefaultKeyGesture,
    bool IsGlobal);

public sealed record ShortcutUpdateResult(bool Success, string? ErrorMessage = null);

public interface IShortcutManagerService
{
    IReadOnlyList<ShortcutItem> GetShortcuts();
    ShortcutUpdateResult UpdateShortcut(string shortcutId, string newKeyGesture);
    void ResetToDefaults();
}

