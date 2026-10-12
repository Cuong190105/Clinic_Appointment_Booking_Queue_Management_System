using Clinic.Application.Contracts;
using Clinic.Application.Interfaces;
using Clinic.Domain.Enums;
using MediatR;

namespace Clinic.Application.Features.Users.Queries
{
    public record GetPagedUsersQuery(
        string? Search = null,
        string? Role = null,
        string SortBy = "fullName",
        Gender? Gender = null,
        bool Descending = false,
        int PageNumber = 1,
        int PageSize = 10
    ) : IRequest<PaginationResponse<UserSummaryDto>>;
    public class GetPagedUsersQueryHandler : IRequestHandler<GetPagedUsersQuery, PaginationResponse<UserSummaryDto>>
    {
        private readonly IUserRepository _userRepository;
        public GetPagedUsersQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        public async Task<PaginationResponse<UserSummaryDto>> Handle(GetPagedUsersQuery request, CancellationToken cancellationToken)
        {
            var users = await _userRepository.GetPagedAsync(
                request.Search,
                request.SortBy,
                request.Role,
                request.Gender,
                request.Descending,
                request.PageNumber,
                request.PageSize
            );
            return new PaginationResponse<UserSummaryDto>
            {
                Items = users.Items.Select(user => new UserSummaryDto
                {
                    UserId = user.Id,
                    Username = user.Username,
                    FullName = user.Person.FullName,
                    PhoneNumber = user.Person.PhoneNumber,
                    Email = user.Person.Email,
                    Gender = user.Person.Gender,
                    Roles = user.UserRoles.Select(ur => ur.Role.Name).ToList(),
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                }).ToList(),
                TotalCount = users.TotalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }
    }
}
