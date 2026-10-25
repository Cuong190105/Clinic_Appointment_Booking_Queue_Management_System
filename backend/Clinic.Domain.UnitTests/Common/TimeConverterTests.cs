using Clinic.Domain.Common;

namespace Clinic.Domain.UnitTests.Common;

public sealed class TimeConverterTests
{
    [Fact]
    public void ToLocalDate_UsesConfiguredZoneAcrossMidnight()
    {
        var converter = new TimeConverter("America/Los_Angeles");

        Assert.Equal(new DateOnly(2026, 1, 1), converter.ToLocalDate(new DateTime(2026, 1, 2, 7, 59, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateOnly(2026, 1, 2), converter.ToLocalDate(new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ConvertToUtc_SupportsEarlyClinicShift()
    {
        var converter = new TimeConverter("Asia/Ho_Chi_Minh");
        var utc = converter.ConvertToUtc(new DateTime(2026, 1, 2, 6, 30, 0));

        Assert.Equal(new DateTime(2026, 1, 1, 23, 30, 0, DateTimeKind.Utc), utc);
        Assert.Equal(new DateOnly(2026, 1, 2), converter.ToLocalDate(utc));
    }

    [Fact]
    public void Constructor_RejectsUnknownConfiguredZone()
    {
        Assert.Throws<TimeZoneNotFoundException>(() => new TimeConverter("Not/A_Timezone"));
    }
}
