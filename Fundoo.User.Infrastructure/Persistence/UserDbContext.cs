using Fundoo.User.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.User.Infrastructure.Persistence;

public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.NormalizedEmail).IsUnique();   // DB-level uniqueness
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.NormalizedEmail).HasMaxLength(256);
        });
    }
}