using Clinic.Infrastructure.Sqlserver.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Sqlserver.Repositories
{
    public class DatabaseInitializer
    {
        private readonly ApplicationDbContext _context;
        public DatabaseInitializer(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task MigrateAsync()
        {
            await _context.Database.MigrateAsync();
        }
    }
}
