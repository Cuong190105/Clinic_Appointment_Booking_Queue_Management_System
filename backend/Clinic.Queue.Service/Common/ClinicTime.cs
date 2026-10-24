namespace Clinic.Queue.Service.Common;

public static class ClinicTime
{
    private static readonly Lazy<TimeZoneInfo> ClinicTz = new(() =>
    {
        var timeZoneId = Environment.GetEnvironmentVariable("CLINIC_TIME_ZONE") ?? "Asia/Ho_Chi_Minh";
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    });

    public static TimeZoneInfo TimeZone => ClinicTz.Value;

    public static DateOnly GetLocalQueueDate(DateTimeOffset utcInstant)
    {
        var localTime = TimeZoneInfo.ConvertTime(utcInstant, TimeZone);
        return DateOnly.FromDateTime(localTime.DateTime);
    }

    public static DateOnly GetTodayQueueDate()
    {
        return GetLocalQueueDate(DateTimeOffset.UtcNow);
    }
}
