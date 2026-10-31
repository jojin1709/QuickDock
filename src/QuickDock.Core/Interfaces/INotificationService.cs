namespace QuickDock.Core.Interfaces;

public interface INotificationService
{
    void ShowNotification(string title, string message, string? action = null);
}

