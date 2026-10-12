using Clinic.Application.Interfaces;
using Clinic.Domain.Entities;
using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class DatabaseInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IUserService _userService;
        private readonly IRoleRepository _roleRepository;
        private readonly IPersonService _personService;
        
        public DatabaseInitializer(ApplicationDbContext context, IConfiguration configuration, IUserService userService, IRoleRepository roleRepository, IPersonService personService)
        {
            _context = context;
            _configuration = configuration;
            _userService = userService;
            _roleRepository = roleRepository;
            _personService = personService;
        }
        public async Task CreateInitialAdminAsync(string? username = null, string? password = null)
        {
            var adminUsername = username ?? _configuration["InitialAdmin:Username"] ?? "admin";
            var adminPassword = password ?? _configuration["InitialAdmin:Password"] ?? "AdminPassowrd123!";

            if (await _context.Users.AnyAsync(u => u.Username == adminUsername))
            {
                return;
            }

            var person = await _personService.CreateOrGetPersonAsync(
                "System Administrator",
                _configuration["InitialAdmin:PhoneNumber"] ?? "0111111111",
                _configuration["InitialAdmin:Email"] ?? "admin@clinic.local",
                new DateOnly(1990, 1, 1),
                Domain.Enums.Gender.Male,
                "Ho Chi Minh City");

            var user = await _userService.CreateUserAsync(adminUsername, adminPassword, person);
            var employee = new Employee(person, new DateOnly(2025, 1, 1));

            var adminRole = await _roleRepository.GetByNameAsync("Admin");
            if (adminRole == null)
            {
                throw new InvalidOperationException("Admin role not found");
            }
            user.AssignRole(adminRole);
            await _context.AddAsync(employee);
            await _context.SaveChangesAsync();
        }
    }
}
