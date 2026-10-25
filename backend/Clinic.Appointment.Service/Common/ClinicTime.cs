namespace Clinic.Appointment.Service.Common;

public static class ClinicTime
{
    private static readonly Lazy<TimeZoneInfo> ConfiguredTimeZone = new(() =>
    {
        var id = Environment.GetEnvironmentVariable("CLINIC_TIME_ZONE") ?? "Asia/Ho_Chi_Minh";
        return TimeZoneInfo.FindSystemTimeZoneById(id);
    });

    public static TimeZoneInfo TimeZone => ConfiguredTimeZone.Value;

    public static DateOnly ToLocalDate(DateTime utcDateTime)
    {
        var utc = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZone));
    }
}
