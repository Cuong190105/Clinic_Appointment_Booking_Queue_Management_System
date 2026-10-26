using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Auth.Commands;

public sealed record IdentityRegistrationProfileIds(Guid PersonId, Guid PatientId, Guid? EmployeeId = null, Guid? DoctorId = null);

public sealed record EnsureIdentityRegistrationProfileCommand(
    string FullName, string PhoneNumber, string? Email, DateOnly DateOfBirth, Gender Gender, string Address)
    : IRequest<IdentityRegistrationProfileIds>;

public sealed class EnsureIdentityRegistrationProfileHandler(IPersonService people, IPersonRepository personRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<EnsureIdentityRegistrationProfileCommand, IdentityRegistrationProfileIds>
{
    public async Task<IdentityRegistrationProfileIds> Handle(
        EnsureIdentityRegistrationProfileCommand request, CancellationToken cancellationToken)
    {
        var person = await personRepository.GetByPhoneNumberAsync(request.PhoneNumber.Trim());
        if (person?.IdentityUserId is not null)
        {
            throw new ArgumentException("A user account already exists for this phone number. Use the from-user endpoint.");
        }
        person ??= await people.CreateOrGetPersonAsync(request.FullName, request.PhoneNumber,
            request.Email, request.DateOfBirth, request.Gender, request.Address);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new IdentityRegistrationProfileIds(person.Id, person.Patient?.Id ?? Guid.NewGuid(), null, null);
    }
}

public sealed record ProvisionIdentityPatientCommand(
    Guid UserId,
    Guid PersonId,
    Guid PatientId,
    string FullName,
    string PhoneNumber,
    string? Email,
    DateOnly DateOfBirth,
    Gender Gender,
    string Address) : IRequest<Guid>;

public sealed class ProvisionIdentityPatientHandler(
    IPersonRepository people,
    IPatientRepository patients,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ProvisionIdentityPatientCommand, Guid>
{
    public async Task<Guid> Handle(ProvisionIdentityPatientCommand request, CancellationToken cancellationToken)
    {
        var person = await people.GetByIdAsync(request.PersonId);
        if (person is null)
        {
            var personWithPhone = await people.GetByPhoneNumberAsync(request.PhoneNumber);
            if (personWithPhone is not null && personWithPhone.Id != request.PersonId)
                throw new InvalidOperationException("Identity and Clinic person IDs do not match for this phone number.");

            person = personWithPhone ?? new Person(
                request.PersonId, request.FullName, request.PhoneNumber, request.Email,
                request.DateOfBirth, request.Gender, request.Address, false, DateTime.UtcNow, null);
            if (personWithPhone is null) await people.AddAsync(person);
        }
        else if (!string.Equals(person.PhoneNumber, request.PhoneNumber, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Identity and Clinic person profiles do not match.");
        }

        var patient = await patients.GetByPersonIdAsync(person.Id);
        if (patient is null)
        {
            await patients.AddAsync(new Patient(
                request.PatientId, person.Id, null, null, DateTime.UtcNow, null, false));
        }
        else if (patient.Id != request.PatientId)
        {
            throw new InvalidOperationException("Identity and Clinic patient IDs do not match.");
        }

        person.LinkIdentityUser(request.UserId);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return patient?.Id ?? request.PatientId;
    }
}
