using Microsoft.EntityFrameworkCore;
using UserManagementApp.Models;

namespace UserManagementApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasKey(p => p.Id);
                e.Property(p => p.Name).HasMaxLength(100).IsRequired();
                e.Property(p => p.Email).HasMaxLength(200).IsRequired();
                e.Property(p => p.Password).HasMaxLength(255).IsRequired();
                e.Property(p => p.Status).HasMaxLength(20).IsRequired();
                e.Property(p => p.VerificationToken).HasMaxLength(510);
                e.HasIndex(p => p.Email).IsUnique().HasDatabaseName("IX_Users_Email");
            });
        }
    }
}
