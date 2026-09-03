using System.Globalization;

namespace GameServer.Web.Services;

public interface IUserTimeZoneService
{
    TimeZoneInfo? UserTimeZone { get; }
    CultureInfo? UserCulture { get; }
    bool IsInitialized { get; }

    ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default);
    DateTime ConvertToUserLocalTime(DateTime utcDateTime);
    string FormatLocalDateTime(DateTime utcDateTime, string? format = "g");
}
