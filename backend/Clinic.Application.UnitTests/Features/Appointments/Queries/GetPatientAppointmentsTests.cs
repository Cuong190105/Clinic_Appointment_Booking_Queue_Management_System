using Clinic.Application.Common.Exceptions;
using Clinic.Application.Features.Appointments.Queries;
using Clinic.Application.UnitTests.Common;
using Clinic.Domain.Enums;

namespace Clinic.Application.UnitTests.Features.Appointments.Queries
{
    public class GetPatientAppointmentsTests
    {
        [Fact]
        public async Task TestGetPatientAppointments()
        {
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var userRepository = new FakeUserRepository();
            var handler = new GetPatientAppointmentsQueryHandler(appointmentRepository, patientRepository, userRepository);
            var patient = TestDataFactory.CreatePatient();
            var workSchedule = TestDataFactory.CreateWorkSchedule(date: DateOnly.FromDateTime(DateTime.UtcNow));
            var appointment1 = TestDataFactory.CreateAppointment(patient: patient, workSchedule: workSchedule, checkedIn: true);
            var appointment2 = TestDataFactory.CreateAppointment(patient: patient, workSchedule: workSchedule, timeSlot: workSchedule.ShiftStart.AddMinutes(30));
            appointment1.Complete(patient.Person.User.Id);
            await appointmentRepository.AddAsync(appointment1);
            await appointmentRepository.AddAsync(appointment2);
            await patientRepository.AddAsync(patient);
            await userRepository.AddAsync(patient.Person.User);
            var query = new GetPatientAppointmentsQuery(patient.Person.User.Id);
            var result = await handler.Handle(query, CancellationToken.None);
            Assert.Single(result);
            Assert.Equal(AppointmentStatus.Pending, result[0].Status);

            var queryCompleted = new GetPatientAppointmentsQuery(patient.Person.User.Id, "Completed");
            var resultCompleted = await handler.Handle(queryCompleted, CancellationToken.None);
            Assert.Single(resultCompleted);
            Assert.Equal(AppointmentStatus.Completed, resultCompleted[0].Status);
        }

        [Fact]
        public async Task TestGetPatientAppointmentsForNonExistentPatient()
        {
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var userRepository = new FakeUserRepository();
            var handler = new GetPatientAppointmentsQueryHandler(appointmentRepository, patientRepository, userRepository);
            var receptionist = TestDataFactory.CreateEmployee(role: "Receptionist");
            var appointment = TestDataFactory.CreateAppointment();
            await appointmentRepository.AddAsync(appointment);
            await userRepository.AddAsync(receptionist.Person.User);
            var query = new GetPatientAppointmentsQuery(receptionist.Person.User.Id, "Completed", Guid.NewGuid());
            await Assert.ThrowsAsync<NotFoundException>(async () => await handler.Handle(query, CancellationToken.None));
        }

        [Fact]
        public async Task TestUnauthorizedGetPatientAppointmentsForPatient()
        {
            var patientRepository = new FakePatientRepository();
            var appointmentRepository = new FakeAppointmentRepository();
            var userRepository = new FakeUserRepository();
            var handler = new GetPatientAppointmentsQueryHandler(appointmentRepository, patientRepository, userRepository);
            var patient = TestDataFactory.CreatePatient();
            var appointment = TestDataFactory.CreateAppointment(patient: patient);
            await appointmentRepository.AddAsync(appointment);

            var query = new GetPatientAppointmentsQuery(Guid.NewGuid());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await handler.Handle(query, CancellationToken.None));
            var query2 = new GetPatientAppointmentsQuery(Guid.NewGuid(), "Completed", patient.Person.User.Id);
        }
    }
}
