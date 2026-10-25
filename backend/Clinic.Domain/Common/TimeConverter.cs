namespace Clinic.Domain.Common
{
    public class TimeConverter
    {
        private readonly TimeZoneInfo _clinicTimeZone;

        public TimeConverter(string? timeZoneId = null)
        {
            var configuredId = timeZoneId ?? Environment.GetEnvironmentVariable("CLINIC_TIME_ZONE") ?? "Asia/Ho_Chi_Minh";
            _clinicTimeZone = TimeZoneInfo.FindSystemTimeZoneById(configuredId);
        }

        public DateTime ConvertToUtc(DateTime localDateTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(localDateTime, _clinicTimeZone);
        }

        public DateTime ConvertFromUtc(DateTime utcDateTime)
        {
            var utc = utcDateTime.Kind == DateTimeKind.Utc ? utcDateTime : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _clinicTimeZone);
        }

        public DateOnly ToLocalDate(DateTime dateTime)
        {
            var utc = dateTime.Kind == DateTimeKind.Utc ? dateTime : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, _clinicTimeZone));
        }

        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _clinicTimeZone));
    }
}
