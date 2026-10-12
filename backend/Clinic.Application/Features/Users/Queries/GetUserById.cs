using Clinic.Application.Common.Exceptions;
using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Users.Queries
{
    public record GetUserQuery(Guid Id) : IRequest<UserDetailDto>;
    public class GetUserByIdQueryHandler : IRequestHandler<GetUserQuery, UserDetailDto>
    {
        private readonly IUserRepository _userRepository;
        public GetUserByIdQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<UserDetailDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.Id);
            if (user == null)
            {
                throw new NotFoundException("User not found");
            }
            return new UserDetailDto
            {
                UserId = user.Id,
                PersonId = user.PersonId,
                Username = user.Username,
                FullName = user.Person.FullName,
                PhoneNumber = user.Person.PhoneNumber,
                Email = user.Person.Email,
                Gender = user.Person.Gender,
                Address = user.Person.Address,
                Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                DateOfBirth = user.Person.DateOfBirth,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
            };
        }
    }
}
