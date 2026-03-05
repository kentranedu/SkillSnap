namespace SkillSnap.Client.Services;

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}

public class NotificationService
{
    public event Action<ToastLevel, string>? OnNotify;

    public void ShowInfo(string message) => Notify(ToastLevel.Info, message);

    public void ShowSuccess(string message) => Notify(ToastLevel.Success, message);

    public void ShowWarning(string message) => Notify(ToastLevel.Warning, message);

    public void ShowError(string message) => Notify(ToastLevel.Error, message);

    private void Notify(ToastLevel level, string message)
    {
        OnNotify?.Invoke(level, message);
    }
}
