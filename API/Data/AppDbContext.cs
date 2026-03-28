using Microsoft.EntityFrameworkCore;
using StartRef.Api.Data.Entities;

namespace StartRef.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Status> Statuses => Set<Status>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Runner> Runners => Set<Runner>();
    public DbSet<ChangeLogEntry> ChangeLogEntries => Set<ChangeLogEntry>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Club> Clubs => Set<Club>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Status>(entity =>
        {
            entity.ToTable("Statuses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).IsRequired().HasMaxLength(50);
            entity.HasData(
                new Status { Id = 1, Name = "Registered" },
                new Status { Id = 2, Name = "Started" },
                new Status { Id = 3, Name = "DNS" }
            );
        });

        modelBuilder.Entity<Competition>(entity =>
        {
            entity.ToTable("Competitions");
            entity.HasKey(x => x.Date);
        });

        modelBuilder.Entity<Runner>(entity =>
        {
            entity.ToTable("Runners");
            entity.HasKey(x => new { x.CompetitionDate, x.StartNumber });

            entity.HasOne(x => x.Competition)
                .WithMany(c => c.Runners)
                .HasForeignKey(x => x.CompetitionDate);

            entity.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId);

            entity.HasIndex(x => new { x.CompetitionDate, x.LastModifiedUtc });

            entity.Property(x => x.StatusId).HasDefaultValue(1);
            entity.Property(x => x.StartPlace).HasDefaultValue(0);
        });

        modelBuilder.Entity<ChangeLogEntry>(entity =>
        {
            entity.ToTable("ChangeLogEntries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).UseIdentityColumn();
            entity.HasIndex(x => new { x.CompetitionDate, x.StartNumber });
            entity.HasIndex(x => x.ChangedAtUtc);
        });

        modelBuilder.Entity<Class>(entity =>
        {
            entity.ToTable("Classes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Club>(entity =>
        {
            entity.ToTable("Clubs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });
    }
}
