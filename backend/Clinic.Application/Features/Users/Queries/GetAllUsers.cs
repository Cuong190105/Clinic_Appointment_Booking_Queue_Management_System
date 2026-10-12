using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace Clinic.Application.Features.Users.Queries
{
    public record GetAllUsersQuery() : IRequest<IReadOnlyList<UserBriefDto>>;
    public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, IReadOnlyList<UserBriefDto>>
    {
        private readonly IUserRepository _userRepository;
        public GetAllUsersQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        public async Task<IReadOnlyList<UserBriefDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
        {
            var users = await _userRepository.GetAllAsync();
            return users.Select(u => new UserBriefDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.Person.FullName
            }).ToList();
        }
    }
}
