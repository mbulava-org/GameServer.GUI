using System.Globalization;
using Microsoft.JSInterop;

namespace GameServer.Web.Services;

public sealed class UserTimeZoneService : IUserTimeZoneService
{
    private readonly IJSRuntime _jsRuntime;

    public TimeZoneInfo? UserTimeZone { get; private set; }
    public CultureInfo? UserCulture { get; private set; }
    public bool IsInitialized { get; private set; }

    public UserTimeZoneService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    public async ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return false;
        }

        try
        {
            var timeZoneId = await _jsRuntime.InvokeAsync<string>("timeZoneHelper.getUserTimeZone", cancellationToken);
            if (!string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var tz))
            {
                UserTimeZone = tz;
            }

            var locale = await _jsRuntime.InvokeAsync<string>("timeZoneHelper.getUserLocale", cancellationToken);
            if (!string.IsNullOrWhiteSpace(locale))
            {
                try
                {
                    UserCulture = CultureInfo.GetCultureInfo(locale);
                }
                catch (CultureNotFoundException)
                {
                    // Fall back to current culture if user locale is not recognized
                }
            }

            IsInitialized = true;
            return true;
        }
        catch
        {
            // JS interop may not be available (prerendering, unit testing without mock, etc.)
            IsInitialized = true;
            return false;
        }
    }

    public DateTime ConvertToUserLocalTime(DateTime utcDateTime)
    {
        var utc = utcDateTime.Kind switch
        {
            DateTimeKind.Utc => utcDateTime,
            DateTimeKind.Local => utcDateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)
        };

        if (UserTimeZone != null)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, UserTimeZone);
        }

        return utc.ToLocalTime();
    }

    public string FormatLocalDateTime(DateTime utcDateTime, string? format = "g")
    {
        if (utcDateTime == default)
        {
            return string.Empty;
        }

        var localTime = ConvertToUserLocalTime(utcDateTime);
        var culture = UserCulture ?? CultureInfo.CurrentCulture;
        return localTime.ToString(string.IsNullOrEmpty(format) ? "g" : format, culture);
    }
}
