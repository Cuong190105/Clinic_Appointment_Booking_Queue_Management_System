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
        internal const int DedicatedPatientCount = 30;
        internal const int PersonCount = DoctorCount + AdminCount + ReceptionistCount + DedicatedPatientCount;

        internal static Guid PersonId(int n) => Guid.Parse($"a0000001-0000-0000-0000-{n:D12}");
        internal static Guid UserId(int n) => Guid.Parse($"a0000002-0000-0000-0000-{n:D12}");
        internal static Guid EmployeeId(int n) => Guid.Parse($"a0000003-0000-0000-0000-{n:D12}");
        internal static Guid DoctorRecordId(int n) => Guid.Parse($"a0000004-0000-0000-0000-{n:D12}");
        internal static Guid PatientRecordId(int n) => Guid.Parse($"a0000005-0000-0000-0000-{n:D12}");
        internal static Guid WorkScheduleId(int n) => Guid.Parse($"a0000008-0000-0000-0000-{n:D12}");
        internal static Guid AppointmentId(int n) => Guid.Parse($"a0000009-0000-0000-0000-{n:D12}");
        internal static Guid QueueTicketId(int n) => Guid.Parse($"a0000010-0000-0000-0000-{n:D12}");
        internal static Guid MedicalReportId(int n) => Guid.Parse($"a0000011-0000-0000-0000-{n:D12}");

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

        internal static Patient[] Patients => Enumerable.Range(1, DedicatedPatientCount).Select(CreatePatient).ToArray();

        private static readonly DateOnly BaseDate = new(2026, 8, 20);
        private static readonly (TimeOnly Start, TimeOnly End) MorningShift = (new TimeOnly(7, 30), new TimeOnly(11, 30));
        private static readonly (TimeOnly Start, TimeOnly End) AfternoonShift = (new TimeOnly(13, 30), new TimeOnly(17, 30));
        private static readonly string[] SpecialtyNames = ["Cardiology", "Dermatology", "Pediatrics", "Orthopedics", "General Practice"];
        private static readonly AppointmentStatus[] PastStatuses = [AppointmentStatus.Cancelled, AppointmentStatus.NoShow, AppointmentStatus.Completed];
        private static readonly string[] SampleReasons =
        [
            "General health checkup", "Follow-up consultation", "Chest pain and shortness of breath",
            "Skin rash and itching", "Persistent cough and sore throat", "Severe migraine and dizziness",
            "Joint stiffness and knee pain", "High blood pressure monitoring", "Diabetes routine management",
            "Digestive disorder and stomach pain", "Annual wellness physical exam", "Post-treatment review"
        ];
        private static readonly (string Symptoms, string Diagnosis, string Prescription, string Notes)[] SampleMedicalCases =
        [
            ("Mild fever, dry cough, and fatigue for 2 days.", "Acute viral upper respiratory infection", "Paracetamol 500mg as needed", "Rest and drink warm fluids."),
            ("Intermittent chest tightness during physical exertion.", "Non-cardiac chest discomfort", "Aspirin 81mg daily", "Follow up in 2 weeks."),
            ("Erythematous papules and intense itching.", "Acute contact dermatitis", "Hydrocortisone cream 1%", "Avoid suspected contact allergens."),
            ("Bilateral knee pain and morning stiffness.", "Early primary osteoarthritis", "Meloxicam 7.5mg daily", "Low-impact exercise recommended."),
            ("Throbbing headache, mild nausea, photophobia.", "Tension-type headache with migraine features", "Ibuprofen 400mg as needed", "Maintain sleep and hydration."),
            ("Epigastric burning pain after meals.", "Gastroesophageal reflux disease", "Esomeprazole 40mg daily", "Avoid spicy foods and follow up.")
        ];

        internal static readonly MedicalReport[] MedicalReports = GenerateMedicalReports();

        private static MedicalReport[] GenerateMedicalReports()
        {
            var reports = new List<MedicalReport>();
            var rng = new Random(20260820);
            int[] offsets = [-28, -24, -20, -16, -12, -8, -4, 2, 6, 11];
            var reportNumber = 1;

            for (var doctorIndex = 1; doctorIndex <= DoctorCount; doctorIndex++)
            {
                var doctorId = DoctorRecordId(doctorIndex);
                var doctorName = People[doctorIndex - 1].FullName;
                var specialtyName = SpecialtyNames[(doctorIndex - 1) % SpecialtyNames.Length];

                for (var scheduleIndex = 0; scheduleIndex < offsets.Length; scheduleIndex++)
                {
                    var scheduleDate = BaseDate.AddDays(offsets[scheduleIndex] + ((doctorIndex - 1) % 3));
                    var (shiftStart, shiftEnd) = (doctorIndex + scheduleIndex) % 2 == 0 ? MorningShift : AfternoonShift;
                    var appointmentCount = rng.Next(0, 11);
                    if (appointmentCount == 0 || scheduleDate >= BaseDate) continue;

                    var slots = new List<TimeOnly>();
                    for (var time = shiftStart; time < shiftEnd; time = time.AddMinutes(15)) slots.Add(time);
                    var chosenSlots = slots.OrderBy(_ => rng.Next()).Take(appointmentCount).OrderBy(time => time);

                    foreach (var slot in chosenSlots)
                    {
                        var appointmentStatus = PastStatuses[rng.Next(PastStatuses.Length)];
                        if (appointmentStatus != AppointmentStatus.Completed || rng.Next(100) >= 93) continue;

                        var patientIndex = rng.Next(1, DedicatedPatientCount + 1);
                        var patientId = PatientRecordId(patientIndex);
                        var appointmentId = AppointmentId(reportNumber);
                        var scheduledAt = scheduleDate.ToDateTime(slot);
                        var checkInAt = scheduledAt.AddMinutes(rng.Next(-60, 61));
                        var shiftStartAt = scheduleDate.ToDateTime(shiftStart);
                        var shiftEndAt = scheduleDate.ToDateTime(shiftEnd);
                        checkInAt = checkInAt < shiftStartAt ? shiftStartAt : checkInAt > shiftEndAt ? shiftEndAt : checkInAt;
                        var calledAt = checkInAt.AddMinutes(rng.Next(3, 16));
                        var examStart = calledAt.AddMinutes(rng.Next(0, 6));
                        var duration = rng.Next(100) < 80 ? rng.Next(15, 31) : rng.Next(5, 51);
                        var examEnd = examStart.AddMinutes(duration);
                        if (examEnd > shiftEndAt) examEnd = shiftEndAt;
                        if (examEnd <= examStart) examEnd = examStart.AddMinutes(5);
                        var sample = SampleMedicalCases[rng.Next(SampleMedicalCases.Length)];

                        reports.Add(new MedicalReport(
                            MedicalReportId(reportNumber++), appointmentId, patientId,
                            sample.Symptoms, sample.Diagnosis, sample.Prescription, sample.Notes,
                            examStart, examEnd, MedicalReportStatus.Finalized, CreatedAt, null, false)
                        {
                            DoctorId = doctorId,
                            DoctorName = doctorName,
                            SpecialtyName = specialtyName,
                            ScheduleDate = scheduleDate
                        });
                    }
                }
            }

            return reports.ToArray();
        }
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

        private static Patient CreatePatient(int n)
        {
            var personNumber = DoctorCount + AdminCount + ReceptionistCount + n;
            return new Patient(
                PatientRecordId(n),
                PersonId(personNumber),
                $"BHXH-{n:D6}",
                $"0908{n:D6}",
                CreatedAt,
                null,
                false);
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
