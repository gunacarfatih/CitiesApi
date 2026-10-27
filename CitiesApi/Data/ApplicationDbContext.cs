
using CitiesApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CitiesApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<City> Cities => Set<City>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<City>(entity =>
            {
                entity.ToTable("cities");

                entity.HasKey(c => c.PlateCode);

                entity.Property(c => c.Name)
                    .HasColumnName("name");

                entity.Property(c => c.PlateCode)
                    .HasColumnName("platecode");

                entity.Property(c => c.Description)
                    .HasColumnName("description");

                entity.Property(c => c.IsActive)
                    .HasColumnName("isactive");
            });
        }
    }
}
