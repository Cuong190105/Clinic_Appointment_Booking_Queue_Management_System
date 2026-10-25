namespace Clinic.Appointment.Service.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class AppointmentDbContextFactory : IDesignTimeDbContextFactory<AppointmentDbContext>
{
    public AppointmentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppointmentDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=ClinicAppointmentDB;User Id=sa;Password=ClinicDev!2026StrongPw;TrustServerCertificate=True;Encrypt=False");

        return new AppointmentDbContext(optionsBuilder.Options);
    }
}

