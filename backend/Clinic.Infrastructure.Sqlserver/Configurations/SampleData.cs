using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    /// <summary>
    /// Deterministic sample rows for EF Core HasData.
    /// Legacy sample account rows remain in the old Clinic schema for the one-time Identity importer.
    /// Person layout: doctors 1-15, admins 16-17, receptionists 18-22, patients 23-52.
    /// </summary>
    internal static class SampleData
    {
        internal static readonly DateTime CreatedAt = new(2026, 1, 1, 0, 0, 0);

        internal const int DoctorCount = 15;
        internal const int AdminCount = 2;
        internal const int ReceptionistCount = 5;
        internal const int PatientAccountCount = 30;
        internal const int PersonCount = DoctorCount + AdminCount + ReceptionistCount + PatientAccountCount;

        internal static Guid PersonId(int n) => Guid.Parse($"a0000001-0000-0000-0000-{n:D12}");
        internal static Guid UserId(int n) => Guid.Parse($"a0000002-0000-0000-0000-{n:D12}");
        internal static Guid EmployeeId(int n) => Guid.Parse($"a0000003-0000-0000-0000-{n:D12}");
        internal static Guid DoctorRecordId(int n) => Guid.Parse($"a0000004-0000-0000-0000-{n:D12}");
        internal static Guid WorkScheduleId(int n) => Guid.Parse($"a0000008-0000-0000-0000-{n:D12}");
        internal static Guid AppointmentId(int n) => Guid.Parse($"a0000009-0000-0000-0000-{n:D12}");
        internal static Guid QueueTicketId(int n) => Guid.Parse($"a0000010-0000-0000-0000-{n:D12}");

        private static readonly (string FullName, Gender Gender, DateOnly Dob)[] People =
        [
            ("Nguyen Van An", Gender.Male, new DateOnly(1978, 4, 12)),
            ("Tran Thi Binh", Gender.Female, new DateOnly(1982, 8, 3)),
            ("Le Minh Chau", Gender.Male, new DateOnly(1975, 1, 21)),
            ("Pham Quoc Dung", Gender.Male, new DateOnly(1980, 11, 9)),
            ("Hoang Thi Em", Gender.Female, new DateOnly(1985, 2, 17)),
            ("Vu Van Phuc", Gender.Male, new DateOnly(1979, 7, 28)),
            ("Dang Thi Giang", Gender.Female, new DateOnly(1983, 5, 6)),
            ("Bui Van Hai", Gender.Male, new DateOnly(1976, 12, 14)),
            ("Ngo Thi Hoa", Gender.Female, new DateOnly(1981, 9, 30)),
            ("Duong Van Khoa", Gender.Male, new DateOnly(1974, 3, 18)),
            ("Luong Thi Lan", Gender.Female, new DateOnly(1984, 6, 22)),
            ("Trinh Van Minh", Gender.Male, new DateOnly(1977, 10, 5)),
            ("Cao Thi Nga", Gender.Female, new DateOnly(1986, 1, 11)),
            ("Phan Van Quang", Gender.Male, new DateOnly(1973, 8, 19)),
            ("Do Thi Quyen", Gender.Female, new DateOnly(1988, 4, 25)),
            ("Vo Thanh Son", Gender.Male, new DateOnly(1972, 2, 8)),
            ("Mai Huu Tam", Gender.Male, new DateOnly(1980, 12, 1)),
            ("Ly Thi Uyen", Gender.Female, new DateOnly(1992, 3, 14)),
            ("Ho Van Vinh", Gender.Male, new DateOnly(1990, 7, 7)),
            ("Dinh Thi Xuan", Gender.Female, new DateOnly(1993, 11, 23)),
            ("Ta Van Yen", Gender.Male, new DateOnly(1989, 5, 16)),
            ("Chau Thi Anh", Gender.Female, new DateOnly(1991, 9, 2)),
            ("Nguyen Thi Bach", Gender.Female, new DateOnly(1995, 1, 4)),
            ("Tran Van Cuong", Gender.Male, new DateOnly(1987, 6, 18)),
            ("Le Thi Dao", Gender.Female, new DateOnly(1998, 8, 9)),
            ("Pham Van Dat", Gender.Male, new DateOnly(1994, 2, 27)),
            ("Hoang Thi Hanh", Gender.Female, new DateOnly(2000, 10, 13)),
            ("Huynh Van Hung", Gender.Male, new DateOnly(1986, 12, 20)),
            ("Phan Thi Kim", Gender.Female, new DateOnly(1996, 4, 8)),
            ("Vu Van Long", Gender.Male, new DateOnly(1991, 7, 31)),
            ("Vo Thi Mai", Gender.Female, new DateOnly(1999, 3, 3)),
            ("Dang Van Nam", Gender.Male, new DateOnly(1985, 5, 15)),
            ("Bui Thi Oanh", Gender.Female, new DateOnly(1997, 9, 21)),
            ("Ngo Van Phat", Gender.Male, new DateOnly(1993, 1, 29)),
            ("Duong Thi Quynh", Gender.Female, new DateOnly(2001, 11, 6)),
            ("Luong Van Sang", Gender.Male, new DateOnly(1984, 8, 12)),
            ("Trinh Thi Trang", Gender.Female, new DateOnly(1992, 2, 2)),
            ("Cao Van Tuan", Gender.Male, new DateOnly(1988, 6, 26)),
            ("Phan Thi Uyen", Gender.Female, new DateOnly(1996, 12, 17)),
            ("Do Van Viet", Gender.Male, new DateOnly(1983, 4, 4)),
            ("Ly Thi Xinh", Gender.Female, new DateOnly(2002, 7, 19)),
            ("Ho Van Yen", Gender.Male, new DateOnly(1990, 10, 10)),
            ("Dinh Thi An", Gender.Female, new DateOnly(1994, 1, 22)),
            ("Ta Van Bao", Gender.Male, new DateOnly(1989, 3, 27)),
            ("Chau Thi Cam", Gender.Female, new DateOnly(1998, 5, 5)),
            ("Nguyen Van Duc", Gender.Male, new DateOnly(1979, 9, 14)),
            ("Tran Thi En", Gender.Female, new DateOnly(2003, 8, 8)),
            ("Le Van Giang", Gender.Male, new DateOnly(1995, 11, 11)),
            ("Pham Thi Hien", Gender.Female, new DateOnly(1982, 2, 14)),
            ("Hoang Van Kiet", Gender.Male, new DateOnly(1997, 6, 1)),
            ("Huynh Thi Linh", Gender.Female, new DateOnly(2000, 12, 24)),
            ("Phan Van My", Gender.Male, new DateOnly(1981, 4, 30))
        ];

        internal static Person[] Persons => Enumerable.Range(1, PersonCount).Select(CreatePerson).ToArray();

        private static Person CreatePerson(int n)
        {
            var info = People[n - 1];
            var username = Username(n);
            var person = new Person(
                PersonId(n),
                info.FullName,
                PhoneNumber(n),
                $"{username}@clinic.local",
                info.Dob,
                info.Gender,
                $"12{n:D2} Nguyen Trai, District {(n % 12) + 1}, Ho Chi Minh City",
                false,
                CreatedAt,
                null);
            person.LinkIdentityUser(UserId(n));
            return person;
        }

        private static string Username(int n)
        {
            if (n <= DoctorCount)
                return $"doctor{n}";
            if (n <= DoctorCount + AdminCount)
                return $"admin{n - DoctorCount}";
            if (n <= DoctorCount + AdminCount + ReceptionistCount)
                return $"receptionist{n - DoctorCount - AdminCount}";
            return $"patient{n - DoctorCount - AdminCount - ReceptionistCount}";
        }

        private static string PhoneNumber(int n) => $"0901{n:D6}";
    }
}
