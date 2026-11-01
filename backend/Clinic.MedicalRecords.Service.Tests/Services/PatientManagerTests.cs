using Clinic.Contracts.Events;
using Clinic.MedicalRecords.Service.DTOs;
using Clinic.MedicalRecords.Service.Persistence;
using Clinic.MedicalRecords.Service.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Clinic.MedicalRecords.Service.Tests.Services;

public class PatientManagerTests
{
    private static MedicalRecordsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MedicalRecordsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new MedicalRecordsDbContext(options);
    }

    [Fact]
    public async Task CreatePatientAsync_PersistsAndReturnsDto()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var request = new CreatePatientRequest(
            FullName: "Nguyen Van A",
            PhoneNumber: "0901234567",
            Email: "nguyenvana@example.com",
            DateOfBirth: new DateOnly(1990, 5, 12),
            Gender: "Male",
            Address: "123 Le Loi, Q1, HCMC",
            InsuranceNumber: "DN4791234567890",
            EmergencyContact: "0909876543 (Vo)"
        );

        var dto = await manager.CreatePatientAsync(request);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal(request.FullName, dto.FullName);
        Assert.Equal(request.PhoneNumber, dto.PhoneNumber);
        Assert.Equal(request.Email, dto.Email);
        Assert.Equal(request.DateOfBirth, dto.DateOfBirth);
        Assert.Equal(request.Gender, dto.Gender);
        Assert.Equal(request.Address, dto.Address);
        Assert.Equal(request.InsuranceNumber, dto.InsuranceNumber);
        Assert.Equal(request.EmergencyContact, dto.EmergencyContact);

        var persisted = await db.Patients.FindAsync(dto.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Nguyen Van A", persisted.FullName);
    }

    [Fact]
    public async Task GetPagedPatientsAsync_FiltersAndPaginates()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        await manager.CreatePatientAsync(new CreatePatientRequest("Tran Thi B", "0911111111", "b@test.com", new DateOnly(1985, 1, 1), "Female", "Hanoi", "DN123", "0900000001"));
        await manager.CreatePatientAsync(new CreatePatientRequest("Le Van C", "0922222222", "c@test.com", new DateOnly(1992, 2, 2), "Male", "Danang", "DN456", "0900000002"));
        await manager.CreatePatientAsync(new CreatePatientRequest("Tran Van D", "0933333333", "d@test.com", new DateOnly(2000, 3, 3), "Male", "Hanoi", "DN789", "0900000003"));

        var resultAll = await manager.GetPagedPatientsAsync(null, null, "asc", 1, 10);
        Assert.Equal(3, resultAll.TotalCount);
        Assert.Equal(3, resultAll.Items.Count);

        var resultFilter = await manager.GetPagedPatientsAsync("Tran", "fullName", "asc", 1, 10);
        Assert.Equal(2, resultFilter.TotalCount);
        Assert.All(resultFilter.Items, p => Assert.Contains("Tran", p.FullName));

        var resultPaging = await manager.GetPagedPatientsAsync(null, null, "asc", 1, 2);
        Assert.Equal(2, resultPaging.Items.Count);
        Assert.Equal(3, resultPaging.TotalCount);
        Assert.Equal(2, resultPaging.TotalPages);
    }

    [Fact]
    public async Task GetPatientByIdAsync_ReturnsCorrectDto()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var created = await manager.CreatePatientAsync(new CreatePatientRequest("Pham Van E", "0944444444", "e@test.com", new DateOnly(1995, 6, 15), "Male", "Hue", null, null));

        var fetched = await manager.GetPatientByIdAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Pham Van E", fetched.FullName);

        var missing = await manager.GetPatientByIdAsync(Guid.NewGuid());
        Assert.Null(missing);
    }

    [Fact]
    public async Task UpdatePatientAsync_UpdatesContactAndInsurance()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var created = await manager.CreatePatientAsync(new CreatePatientRequest("Hoang Thi F", "0955555555", "f@test.com", new DateOnly(1993, 4, 10), "Female", "Can Tho", "OLD_INS", "OLD_EMERGENCY"));

        var updateReq = new UpdatePatientRequest(
            Email: "f_updated@test.com",
            Gender: "Female",
            Address: "456 Tran Hung Dao, Can Tho",
            InsuranceNumber: "NEW_INS_999",
            EmergencyContact: "0988888888 (Me)"
        );

        var updated = await manager.UpdatePatientAsync(created.Id, updateReq);
        Assert.True(updated);

        var fetched = await manager.GetPatientByIdAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal("f_updated@test.com", fetched.Email);
        Assert.Equal("456 Tran Hung Dao, Can Tho", fetched.Address);
        Assert.Equal("NEW_INS_999", fetched.InsuranceNumber);
        Assert.Equal("0988888888 (Me)", fetched.EmergencyContact);
    }

    [Fact]
    public async Task HandleUserProfileUpdatedAsync_SynchronizesPatientIdentityProjection()
    {
        using var db = CreateDbContext();
        var manager = new MedicalRecordsManager(db, NullLogger<MedicalRecordsManager>.Instance);

        var patientId = Guid.NewGuid();
        var identityUserId = Guid.NewGuid();
        var personId = Guid.NewGuid();

        var patient = new Entities.Patient(
            id: patientId,
            identityUserId: identityUserId,
            fullName: "Old Name",
            phoneNumber: "0900000000",
            email: "old@test.com",
            dateOfBirth: new DateOnly(1990, 1, 1),
            gender: "Male",
            address: "Old Address",
            insuranceNumber: "INS001",
            emergencyContact: null
        ) { PersonId = personId };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var evt = new UserProfileUpdated(
            PersonId: personId,
            FullName: "New Synchronized Name",
            PhoneNumber: "0987654321",
            Email: "new@test.com",
            Gender: "Male",
            DateOfBirth: new DateOnly(1990, 1, 1),
            Address: "New Synchronized Address",
            PatientId: patientId,
            IdentityUserId: identityUserId
        );

        var result = await manager.HandleUserProfileUpdatedAsync(evt);
        Assert.True(result);

        var updated = await db.Patients.FindAsync(patientId);
        Assert.NotNull(updated);
        Assert.Equal("New Synchronized Name", updated.FullName);
        Assert.Equal("0987654321", updated.PhoneNumber);
        Assert.Equal("new@test.com", updated.Email);
        Assert.Equal("New Synchronized Address", updated.Address);
    }
}
