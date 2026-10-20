using Clinic.Identity.Service.Models;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Identity.Service.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<PersonProfile> Persons => Set<PersonProfile>();
    public DbSet<IdentityUser> Users => Set<IdentityUser>();
    public DbSet<IdentityRole> Roles => Set<IdentityRole>();
    public DbSet<IdentityPermission> Permissions => Set<IdentityPermission>();
    public DbSet<IdentityUserRole> UserRoles => Set<IdentityUserRole>();
    public DbSet<IdentityRolePermission> RolePermissions => Set<IdentityRolePermission>();
    public DbSet<IdentityRefreshToken> RefreshTokens => Set<IdentityRefreshToken>();
    public DbSet<IdentityPasswordResetToken> PasswordResetTokens => Set<IdentityPasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PersonProfile>(e =>
        {
            e.ToTable("Persons"); e.HasKey(x => x.Id); e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20); e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Gender).HasMaxLength(30); e.Property(x => x.Address).HasMaxLength(500);
            e.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");
        });
        model.Entity<IdentityUser>(e =>
        {
            e.ToTable("Users"); e.HasKey(x => x.Id); e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired(); e.HasIndex(x => x.Username).IsUnique(); e.HasIndex(x => x.PersonId).IsUnique();
            e.HasOne(x => x.Person).WithMany().HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<IdentityRole>(e => { e.ToTable("Roles"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(50).IsRequired(); e.HasIndex(x => x.Name).IsUnique(); });
        model.Entity<IdentityPermission>(e => { e.ToTable("Permissions"); e.HasKey(x => x.Id); e.Property(x => x.Name).HasMaxLength(150).IsRequired(); e.HasIndex(x => x.Name).IsUnique(); });
        model.Entity<IdentityUserRole>(e => { e.ToTable("UserRoles"); e.HasKey(x => new { x.UserId, x.RoleId }); e.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId); e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId); });
        model.Entity<IdentityRolePermission>(e => { e.ToTable("RolePermissions"); e.HasKey(x => new { x.RoleId, x.PermissionId }); e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId); e.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId); });
        model.Entity<IdentityRefreshToken>(e => { e.ToTable("RefreshTokens"); e.HasKey(x => x.Id); e.Property(x => x.TokenHash).HasMaxLength(500).IsRequired(); e.HasIndex(x => x.TokenHash).IsUnique(); e.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        model.Entity<IdentityPasswordResetToken>(e => { e.ToTable("PasswordResetTokens"); e.HasKey(x => x.Id); e.Property(x => x.TokenHash).HasMaxLength(100).IsRequired(); e.HasIndex(x => x.TokenHash).IsUnique(); e.HasIndex(x => new { x.UserId, x.ExpiresAt }); e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
    }
}
