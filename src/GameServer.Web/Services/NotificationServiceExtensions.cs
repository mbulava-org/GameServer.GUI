namespace Radzen;

/// <summary>
/// Extension methods for <see cref="NotificationService"/> that apply consistent
/// timeout and close behavior across the Blazor UI.
/// </summary>
public static class NotificationServiceExtensions
{
    private const int DefaultDurationMs = 5000;

    /// <summary>
    /// Shows a Radzen notification that automatically dismisses after a timeout
    /// and can be closed manually via the built-in close button.
    /// </summary>
    public static void NotifyTimed(
        this NotificationService notificationService,
        NotificationSeverity severity,
        string summary,
        string? detail = null,
        int? durationMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(notificationService);

        notificationService.Notify(new NotificationMessage
        {
            Severity = severity,
            Summary = summary,
            Detail = detail,
            Duration = durationMilliseconds ?? DefaultDurationMs,
            CloseOnClick = false
        });
    }

    /// <summary>
    /// Shows a Radzen notification from a <paramref name="message"/> and applies a default
    /// timeout if the message does not already specify one.
    /// </summary>
    public static void NotifyTimed(
        this NotificationService notificationService,
        NotificationMessage message,
        int? durationMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(notificationService);
        ArgumentNullException.ThrowIfNull(message);

        if (message.Duration is null or <= 0)
        {
            message.Duration = durationMilliseconds ?? DefaultDurationMs;
        }

        notificationService.Notify(message);
    }
}
