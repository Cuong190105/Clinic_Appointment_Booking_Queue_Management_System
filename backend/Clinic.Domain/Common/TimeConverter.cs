namespace Clinic.Domain.Common
{
    public class TimeConverter
    {
        private static readonly TimeZoneInfo VietnamTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

        public DateTime ConvertToUtc(DateTime localDateTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(localDateTime, VietnamTimeZone);
        }

        public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone));
    }
}
