using Fundoo.User.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.User.Infrastructure;

public class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired();
            entity.Property(u => u.FirstName).IsRequired(false);
            entity.Property(u => u.LastName).IsRequired(false);
            entity.Property(u => u.FullName).IsRequired(false);
            entity.Property(u => u.TwoFactorSecret).IsRequired(false);
            entity.Property(u => u.TwoFactorCode).IsRequired(false);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.AuthProvider).HasDefaultValue("Local");
            entity.Property(u => u.IsTwoFactorEnabled).HasDefaultValue(false);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.HasIndex(s => s.RefreshToken).IsUnique();

            entity.Property(s => s.RefreshToken)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(s => s.IpAddress)
                .HasMaxLength(45);

            entity.Property(s => s.UserAgent)
                .HasMaxLength(500);

            entity.HasOne<UserEntity>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
