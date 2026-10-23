namespace Clinic.Queue.Service.Common;

public static class ClinicTime
{
    private static readonly Lazy<TimeZoneInfo> ClinicTz = new(() =>
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
        }
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
