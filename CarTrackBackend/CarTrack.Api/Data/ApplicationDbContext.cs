using CarTrack.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CarTrack.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Maintenance> Maintenances { get; set; } // Bu satırı ekledik
    public DbSet<FuelRecord> FuelRecords { get; set; }
    public DbSet<VehicleDocument> VehicleDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<VehicleDocument>()
            .HasIndex(document => new { document.VehicleId, document.Type })
            .IsUnique();
    }
}