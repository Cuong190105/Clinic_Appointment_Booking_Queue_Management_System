using Clinic.Domain.Entities;
using Clinic.Domain.Enums;

namespace Clinic.Infrastructure.Sqlserver.Configurations
{
    /// <summary>
    /// Deterministic sample rows for EF Core HasData.
    /// Every seeded user password is <c>Password123!</c>.
    /// Person layout: doctors 1-15, admins 16-17, receptionists 18-22, patients 23-52.
    /// </summary>
    internal static class SampleData
    {
        internal static readonly DateTime CreatedAt = new(2026, 1, 1, 0, 0, 0);

        // BCrypt hash of "Password123!"
        internal const string PasswordHash = "$2a$11$G0m4K.LSMeRwVPfVM1CMOux4zgF46UDGK57iWrKIDIAnH/kqOGv.K";

        internal static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        internal static readonly Guid PatientRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        internal static readonly Guid ReceptionistRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        internal static readonly Guid DoctorRoleId = Guid.Parse("00000000-0000-0000-0000-000000000004");

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
        internal static Guid SpecialtyId(int n) => Guid.Parse($"a0000006-0000-0000-0000-{n:D12}");
        internal static Guid WorkHistoryId(int n) => Guid.Parse($"a0000007-0000-0000-0000-{n:D12}");
        internal static Guid WorkScheduleId(int n) => Guid.Parse($"a0000008-0000-0000-0000-{n:D12}");
        internal static Guid AppointmentId(int n) => Guid.Parse($"a0000009-0000-0000-0000-{n:D12}");
        internal static Guid QueueTicketId(int n) => Guid.Parse($"a0000010-0000-0000-0000-{n:D12}");
        internal static Guid MedicalReportId(int n) => Guid.Parse($"a0000011-0000-0000-0000-{n:D12}");

        internal static readonly Specialty[] Specialties =
        [
            new Specialty(SpecialtyId(1), "Cardiology", "Heart and cardiovascular care", new DateOnly(2010, 1, 15), CreatedAt, null, false),
            new Specialty(SpecialtyId(2), "Dermatology", "Skin, hair, and nail care", new DateOnly(2012, 3, 1), CreatedAt, null, false),
            new Specialty(SpecialtyId(3), "Pediatrics", "Medical care for infants and children", new DateOnly(2008, 6, 20), CreatedAt, null, false),
            new Specialty(SpecialtyId(4), "Orthopedics", "Bones, joints, and musculoskeletal care", new DateOnly(2011, 9, 10), CreatedAt, null, false),
            new Specialty(SpecialtyId(5), "General Practice", "Primary care and general medicine", new DateOnly(2005, 4, 1), CreatedAt, null, false)
        ];

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

        private static readonly string[] DoctorQualifications =
        [
            "MD, Cardiology Specialist I",
            "MD, Cardiology Specialist II",
            "MD, Interventional Cardiology",
            "MD, Dermatology Specialist I",
            "MD, Cosmetic Dermatology",
            "MD, Dermatology Specialist II",
            "MD, Pediatrics Specialist I",
            "MD, Neonatology",
            "MD, Pediatrics Specialist II",
            "MD, Orthopedic Surgery",
            "MD, Sports Medicine",
            "MD, Trauma Orthopedics",
            "MD, Family Medicine",
            "MD, Internal Medicine",
            "MD, General Practice"
        ];

        internal static Person[] Persons => Enumerable.Range(1, PersonCount).Select(CreatePerson).ToArray();

        internal static User[] Users => Enumerable.Range(1, PersonCount).Select(CreateUser).ToArray();

        internal static Employee[] Employees => Enumerable.Range(1, DoctorCount + AdminCount + ReceptionistCount).Select(CreateEmployee).ToArray();

        internal static Doctor[] Doctors => Enumerable.Range(1, DoctorCount).Select(CreateDoctor).ToArray();

        internal static WorkHistory[] WorkHistories => Enumerable.Range(1, DoctorCount).Select(CreateWorkHistory).ToArray();

        internal static Patient[] Patients => Enumerable.Range(1, DedicatedPatientCount).Select(CreatePatient).ToArray();

        internal static readonly WorkSchedule[] WorkSchedules;
        internal static readonly Appointment[] Appointments;
        internal static readonly QueueTicket[] QueueTickets;
        internal static readonly MedicalReport[] MedicalReports;

        private static readonly DateOnly BaseDate = new(2026, 8, 20);

        private static readonly (TimeOnly Start, TimeOnly End) MorningShift = (new TimeOnly(7, 30), new TimeOnly(11, 30));
        private static readonly (TimeOnly Start, TimeOnly End) AfternoonShift = (new TimeOnly(13, 30), new TimeOnly(17, 30));

        private static readonly AppointmentStatus[] PastStatuses =
        [
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow,
            AppointmentStatus.Completed
        ];

        private static readonly AppointmentStatus[] FutureStatuses =
        [
            AppointmentStatus.Cancelled,
            AppointmentStatus.Pending,
            AppointmentStatus.Confirmed
        ];

        private static readonly string[] SampleReasons =
        [
            "General health checkup",
            "Follow-up consultation",
            "Chest pain and shortness of breath",
            "Skin rash and itching",
            "Persistent cough and sore throat",
            "Severe migraine and dizziness",
            "Joint stiffness and knee pain",
            "High blood pressure monitoring",
            "Diabetes routine management",
            "Digestive disorder and stomach pain",
            "Annual wellness physical exam",
            "Post-treatment review",
            "Ear infection and discomfort",
            "Allergic reaction consultation",
            "Pediatric developmental screening",
            "Routine cardiovascular screening",
            "Dermatology cosmetic consultation",
            "Musculoskeletal evaluation"
        ];

        private static readonly (string Symptoms, string Diagnosis, string Prescription, string Notes)[] SampleMedicalCases =
        [
            (
                "Mild fever, dry cough, and fatigue for 2 days.",
                "Acute viral upper respiratory infection",
                "Paracetamol 500mg (1 tab TID prn fever), Vitamin C 500mg (1 tab daily), Cough syrup 10ml TID.",
                "Rest, drink plenty of warm fluids, return if fever persists > 3 days or dyspnea develops."
            ),
            (
                "Intermittent chest tightness during physical exertion, mild palpitations.",
                "Non-cardiac chest discomfort - rule out exertional angina",
                "Aspirin 81mg (1 tab daily), Trimetazidine 35mg (1 tab BID), Nitroglycerin sublingual prn.",
                "Referred for treadmill stress test and 12-lead ECG. Follow up in 2 weeks."
            ),
            (
                "Erythematous papules and intense itching on bilateral forearms and neck.",
                "Acute contact dermatitis",
                "Hydrocortisone cream 1% (apply BID), Cetirizine 10mg (1 tab at bedtime).",
                "Avoid suspected contact allergens, apply hypoallergenic moisturizer, do not scratch lesions."
            ),
            (
                "Bilateral knee pain, stiffness worse in mornings lasting 15 minutes, crepitus on movement.",
                "Early primary osteoarthritis of the knee",
                "Glucosamine sulfate 1500mg (1 packet daily), Meloxicam 7.5mg (1 tab daily with food).",
                "Recommended low-impact exercise (swimming/cycling), physical therapy session scheduled."
            ),
            (
                "Throbbing frontal and temporal headache, mild nausea, photophobia.",
                "Tension-type headache with migraine features",
                "Ibuprofen 400mg (1 tab prn), Magnesium B6 (2 tabs BID).",
                "Advised regular sleep hygiene, adequate hydration, stress reduction techniques."
            ),
            (
                "Epigastric burning pain aggravated after meals, bloating and acid regurgitation.",
                "Gastroesophageal reflux disease (GERD) with mild gastritis",
                "Esomeprazole 40mg (1 tab 30 mins before breakfast), Phosphalugel (1 sachet TID prn).",
                "Avoid spicy/fatty foods, do not lie down within 2 hours after meals, follow up in 4 weeks."
            ),
            (
                "Lower back stiffness and dull ache after heavy lifting, localized tenderness.",
                "Acute lumbar muscle strain",
                "Eperisone 50mg (1 tab TID), Paracetamol 500mg (1 tab TID), Topical diclofenac gel.",
                "Relative rest for 48 hours, avoid heavy lifting, gentle stretching exercises demonstrated."
            ),
            (
                "Routine screening: asymptomatic, BP recorded 145/92 mmHg, BMI 26.2.",
                "Essential hypertension Stage 1",
                "Amlodipine 5mg (1 tab daily in the morning).",
                "Low-sodium DASH diet recommended, home BP logging twice daily, review in 1 month."
            ),
            (
                "Fasting blood glucose 7.4 mmol/L, HbA1c 6.8%, mild polydipsia.",
                "Type 2 diabetes mellitus - newly diagnosed",
                "Metformin 500mg (1 tab BID with meals).",
                "Referred to nutrition specialist, instructed on self-monitoring of blood glucose."
            ),
            (
                "Nasal congestion, clear rhinorrhea, sneezing bouts, itchy eyes.",
                "Allergic rhinitis",
                "Fluticasone furoate nasal spray (1 spray per nostril daily), Fexofenadine 180mg (1 tab daily).",
                "Minimize exposure to dust and pollens, use saline nasal wash daily."
            )
        ];

        static SampleData()
        {
            (WorkSchedules, Appointments, QueueTickets, MedicalReports) = GenerateWorkSchedulesAppointmentsQueueTicketsAndMedicalReports();
        }

        private static (WorkSchedule[], Appointment[], QueueTicket[], MedicalReport[]) GenerateWorkSchedulesAppointmentsQueueTicketsAndMedicalReports()
        {
            var schedules = new List<WorkSchedule>(DoctorCount * 10);
            var appointments = new List<Appointment>();
            var queueTickets = new List<QueueTicket>();
            var medicalReports = new List<MedicalReport>();
            var rng = new Random(20260820);

            // 10 date offsets ranging from past (max 30 days) to near future (max 14 days)
            int[] baseOffsets = [-28, -24, -20, -16, -12, -8, -4, 2, 6, 11];

            var scheduleIdCounter = 1;
            var appointmentIdCounter = 1;
            var queueTicketIdCounter = 1;
            var medicalReportIdCounter = 1;

            for (var doctorIndex = 1; doctorIndex <= DoctorCount; doctorIndex++)
            {
                var doctorId = DoctorRecordId(doctorIndex);

                for (var scheduleIndex = 0; scheduleIndex < 10; scheduleIndex++)
                {
                    var offset = baseOffsets[scheduleIndex] + ((doctorIndex - 1) % 3);
                    var scheduleDate = BaseDate.AddDays(offset);
                    var (shiftStart, shiftEnd) = ((doctorIndex + scheduleIndex) % 2 == 0) ? MorningShift : AfternoonShift;
                    var patientLimit = 20 + ((doctorIndex * 3 + scheduleIndex * 7) % 21); // 20 to 40
                    var scheduleId = WorkScheduleId(scheduleIdCounter++);

                    var schedule = new WorkSchedule(
                        scheduleId,
                        doctorId,
                        scheduleDate,
                        shiftStart,
                        shiftEnd,
                        patientLimit,
                        WorkScheduleStatus.Active,
                        CreatedAt,
                        null,
                        false);

                    schedules.Add(schedule);

                    // Random 0 to 10 appointments per schedule
                    var appointmentCount = rng.Next(0, 11);
                    if (appointmentCount == 0)
                        continue;

                    var availableSlots = new List<TimeOnly>();
                    for (var t = shiftStart; t < shiftEnd; t = t.AddMinutes(15))
                    {
                        availableSlots.Add(t);
                    }

                    // Pick appointmentCount distinct slots
                    var chosenSlots = availableSlots.OrderBy(_ => rng.Next()).Take(appointmentCount).OrderBy(t => t).ToList();

                    var isPast = scheduleDate < BaseDate;
                    var scheduleAppointments = new List<Appointment>();

                    foreach (var slot in chosenSlots)
                    {
                        var patientIndex = rng.Next(1, DedicatedPatientCount + 1);
                        var patientId = PatientRecordId(patientIndex);

                        AppointmentStatus status;
                        bool isWalkIn;

                        if (isPast)
                        {
                            status = PastStatuses[rng.Next(PastStatuses.Length)];
                            isWalkIn = rng.Next(2) == 1;
                        }
                        else
                        {
                            status = FutureStatuses[rng.Next(FutureStatuses.Length)];
                            isWalkIn = false;
                        }

                        var reason = SampleReasons[rng.Next(SampleReasons.Length)];

                        // UpdatedByUserId mix of the patient of that appointment, or a random Receptionist
                        var patientPersonNumber = DoctorCount + AdminCount + ReceptionistCount + patientIndex;
                        var patientUserId = UserId(patientPersonNumber);

                        var receptionistIndex = rng.Next(1, ReceptionistCount + 1);
                        var receptionistPersonNumber = DoctorCount + AdminCount + receptionistIndex;
                        var receptionistUserId = UserId(receptionistPersonNumber);

                        var updatedByUserId = rng.Next(2) == 0 ? patientUserId : receptionistUserId;

                        var appointment = new Appointment(
                            AppointmentId(appointmentIdCounter++),
                            patientId,
                            scheduleId,
                            slot,
                            reason,
                            status,
                            isWalkIn,
                            updatedByUserId,
                            CreatedAt,
                            null,
                            false);

                        appointments.Add(appointment);
                        scheduleAppointments.Add(appointment);
                    }

                    // QueueTickets for this workschedule:
                    // if appointment status is Completed or (NoShow with IsWalkIn true) or (random NoShow with IsWalkIn false)
                    var eligibleAppointments = scheduleAppointments
                        .Where(a => a.Status == AppointmentStatus.Completed
                                 || (a.Status == AppointmentStatus.NoShow && a.IsWalkIn)
                                 || (a.Status == AppointmentStatus.NoShow && !a.IsWalkIn && rng.Next(2) == 0))
                        .OrderBy(a => a.TimeSlot)
                        .ToList();

                    var queueNumber = 1;
                    var shiftStartDt = new DateTime(scheduleDate.Year, scheduleDate.Month, scheduleDate.Day, shiftStart.Hour, shiftStart.Minute, 0);
                    var shiftEndDt = new DateTime(scheduleDate.Year, scheduleDate.Month, scheduleDate.Day, shiftEnd.Hour, shiftEnd.Minute, 0);

                    foreach (var appt in eligibleAppointments)
                    {
                        var apptDt = new DateTime(scheduleDate.Year, scheduleDate.Month, scheduleDate.Day, appt.TimeSlot.Hour, appt.TimeSlot.Minute, 0);

                        DateTime checkInTime;
                        if (appt.IsWalkIn)
                        {
                            checkInTime = apptDt;
                        }
                        else
                        {
                            // random time around the time slot (min before 1 hour and max after 1 hour) and must stay in shift time window
                            var deltaMinutes = rng.Next(-60, 61);
                            checkInTime = apptDt.AddMinutes(deltaMinutes);
                            if (checkInTime < shiftStartDt)
                                checkInTime = shiftStartDt;
                            if (checkInTime > shiftEndDt)
                                checkInTime = shiftEndDt;
                        }

                        // Queue ticket status gets mixed with Completed or Skipped (force Skipped with NoShow)
                        // Make it optimistic by increase the rate of completed queue ticket to 90 - 95%
                        QueueStatus ticketStatus;
                        if (appt.Status == AppointmentStatus.NoShow)
                        {
                            ticketStatus = QueueStatus.Skipped;
                        }
                        else
                        {
                            // 93% completed rate
                            ticketStatus = rng.Next(100) < 93 ? QueueStatus.Completed : QueueStatus.Skipped;
                        }

                        // Priority mixed with true or false
                        var priority = rng.Next(2) == 0;

                        // CalledAt must be null for Skipped, and randomly be after CheckInTime 3-15 minutes for the rest
                        DateTime? calledAt = null;
                        if (ticketStatus != QueueStatus.Skipped)
                        {
                            var delay = rng.Next(3, 16);
                            calledAt = checkInTime.AddMinutes(delay);
                        }

                        var ticket = new QueueTicket(
                            QueueTicketId(queueTicketIdCounter++),
                            appt.Id,
                            queueNumber++,
                            priority,
                            ticketStatus,
                            checkInTime,
                            calledAt,
                            CreatedAt,
                            null,
                            false);

                        queueTickets.Add(ticket);

                        // For each completed queue ticket, create a medical report
                        if (ticketStatus == QueueStatus.Completed)
                        {
                            var called = calledAt ?? checkInTime;
                            // Exam start time: shortly after calledAt (0-5 minutes)
                            var startDelay = rng.Next(0, 6);
                            var examStartTime = called.AddMinutes(startDelay);

                            // Exam end time: vary from 5 minutes later than start time to shift end, with more chance to be 15-30 minutes
                            int durationMinutes;
                            if (rng.Next(100) < 80)
                            {
                                durationMinutes = rng.Next(15, 31); // 15-30 minutes (80% chance)
                            }
                            else
                            {
                                durationMinutes = rng.Next(5, 51); // 5-50 minutes (20% chance)
                            }

                            var examEndTime = examStartTime.AddMinutes(durationMinutes);
                            if (examEndTime > shiftEndDt)
                                examEndTime = shiftEndDt;
                            if (examEndTime <= examStartTime)
                                examEndTime = examStartTime.AddMinutes(5);

                            var sampleCase = SampleMedicalCases[rng.Next(SampleMedicalCases.Length)];

                            var report = new MedicalReport(
                                MedicalReportId(medicalReportIdCounter++),
                                ticket.Id,
                                sampleCase.Symptoms,
                                sampleCase.Diagnosis,
                                sampleCase.Prescription,
                                sampleCase.Notes,
                                examStartTime,
                                examEndTime,
                                MedicalReportStatus.Finalized,
                                CreatedAt,
                                null,
                                false);

                            medicalReports.Add(report);
                        }
                    }
                }
            }

            return (schedules.ToArray(), appointments.ToArray(), queueTickets.ToArray(), medicalReports.ToArray());
        }

        internal static UserRole[] UserRoles
        {
            get
            {
                var roles = new List<UserRole>(PersonCount + DoctorCount + AdminCount + ReceptionistCount);
                for (var n = 1; n <= PersonCount; n++)
                    roles.Add(new UserRole(UserId(n), PatientRoleId));

                for (var n = 1; n <= DoctorCount; n++)
                    roles.Add(new UserRole(UserId(n), DoctorRoleId));

                for (var n = 1; n <= AdminCount; n++)
                    roles.Add(new UserRole(UserId(DoctorCount + n), AdminRoleId));

                for (var n = 1; n <= ReceptionistCount; n++)
                    roles.Add(new UserRole(UserId(DoctorCount + AdminCount + n), ReceptionistRoleId));

                return roles.ToArray();
            }
        }

        private static Person CreatePerson(int n)
        {
            var info = People[n - 1];
            var username = Username(n);
            return new Person(
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
        }

        private static User CreateUser(int n) =>
            new(UserId(n), Username(n), PasswordHash, true, PersonId(n), CreatedAt, null, false);

        private static Employee CreateEmployee(int n) =>
            new(EmployeeId(n), PersonId(n), new DateOnly(2022, 1, 15).AddMonths(n), EmployeeStatus.Active, null, CreatedAt, null, false);

        private static Doctor CreateDoctor(int n) =>
            new(
                DoctorRecordId(n),
                EmployeeId(n),
                $"LIC-{n:D4}",
                DoctorQualifications[n - 1],
                5 + (n % 20),
                $"Sample profile for {People[n - 1].FullName}.",
                DoctorStatus.Active,
                CreatedAt,
                null,
                false);

        private static WorkHistory CreateWorkHistory(int n)
        {
            var specialtyIndex = ((n - 1) / 3) + 1;
            return new WorkHistory(WorkHistoryId(n), DoctorRecordId(n), SpecialtyId(specialtyIndex), new DateOnly(2022, 1, 15).AddMonths(n), null, CreatedAt, null, false);
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
