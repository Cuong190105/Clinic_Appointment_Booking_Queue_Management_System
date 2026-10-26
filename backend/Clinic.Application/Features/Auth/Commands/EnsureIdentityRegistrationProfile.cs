using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Auth.Commands;

public sealed record IdentityRegistrationProfileIds(Guid PersonId, Guid? EmployeeId = null, Guid? DoctorId = null);

public sealed record EnsureIdentityRegistrationProfileCommand(
    string FullName, string PhoneNumber, string? Email, DateOnly DateOfBirth, Gender Gender, string Address)
    : IRequest<IdentityRegistrationProfileIds>;

public sealed class EnsureIdentityRegistrationProfileHandler(
    IPersonService people,
    IPersonRepository personRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<EnsureIdentityRegistrationProfileCommand, IdentityRegistrationProfileIds>
{
    public async Task<IdentityRegistrationProfileIds> Handle(
        EnsureIdentityRegistrationProfileCommand request,
        CancellationToken cancellationToken)
    {
        var person = await personRepository.GetByPhoneNumberAsync(request.PhoneNumber.Trim());
        if (person?.IdentityUserId is not null)
        {
            throw new ArgumentException("A user account already exists for this phone number. Use the from-user endpoint.");
        }

        person ??= await people.CreateOrGetPersonAsync(
            request.FullName,
            request.PhoneNumber,
            request.Email,
            request.DateOfBirth,
            request.Gender,
            request.Address);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new IdentityRegistrationProfileIds(person.Id);
    }
}
