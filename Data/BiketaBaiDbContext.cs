using Microsoft.EntityFrameworkCore;
using BiketaBai.Models;

namespace BiketaBai.Data;

public class BiketaBaiDbContext : DbContext
{
    public BiketaBaiDbContext(DbContextOptions<BiketaBaiDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<BikeType> BikeTypes { get; set; }
    public DbSet<Bike> Bikes { get; set; }
    public DbSet<BikeImage> BikeImages { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Rating> Ratings { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<PhoneOtp> PhoneOtps { get; set; }
    public DbSet<LocationTracking> LocationTracking { get; set; }
    public DbSet<Store> Stores { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure decimal precision for currency fields
        modelBuilder.Entity<Bike>()
            .Property(b => b.HourlyRate)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Bike>()
            .Property(b => b.DailyRate)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Booking>()
            .Property(b => b.RentalHours)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Booking>()
            .Property(b => b.BaseRate)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Booking>()
            .Property(b => b.ServiceFee)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Booking>()
            .Property(b => b.TotalAmount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Booking>()
            .Property(b => b.DistanceSavedKm)
            .HasPrecision(10, 2);

        modelBuilder.Entity<LocationTracking>()
            .Property(lt => lt.DistanceFromStoreKm)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.RefundAmount)
            .HasPrecision(10, 2);

        // Configure relationships
        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Renter)
            .WithMany(u => u.BookingsAsRenter)
            .HasForeignKey(b => b.RenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Rating>()
            .HasOne(r => r.Rater)
            .WithMany(u => u.RatingsGiven)
            .HasForeignKey(r => r.RaterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Rating>()
            .HasOne(r => r.RatedUser)
            .WithMany(u => u.RatingsReceived)
            .HasForeignKey(r => r.RatedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.OwnerVerifier)
            .WithMany(u => u.PaymentsVerified)
            .HasForeignKey(p => p.OwnerVerifiedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Store>()
            .HasOne(s => s.Owner)
            .WithMany(u => u.Stores)
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Bike>()
            .HasOne(b => b.Store)
            .WithMany(s => s.Bikes)
            .HasForeignKey(b => b.StoreId)
            .OnDelete(DeleteBehavior.SetNull);

        // Seed data for bike types
        modelBuilder.Entity<BikeType>().HasData(
            new BikeType { BikeTypeId = 1, TypeName = "Mountain Bike", Description = "Off-road cycling" },
            new BikeType { BikeTypeId = 2, TypeName = "Road Bike", Description = "Paved road cycling" },
            new BikeType { BikeTypeId = 3, TypeName = "Hybrid Bike", Description = "Versatile for various terrains" },
            new BikeType { BikeTypeId = 4, TypeName = "Electric Bike", Description = "E-bike with motor assistance" },
            new BikeType { BikeTypeId = 5, TypeName = "City/Commuter Bike", Description = "Urban commuting" },
            new BikeType { BikeTypeId = 6, TypeName = "BMX", Description = "Tricks and stunts" },
            new BikeType { BikeTypeId = 7, TypeName = "Folding Bike", Description = "Compact and portable" }
        );

        // Create indexes for better performance
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Bike>()
            .HasIndex(b => b.AvailabilityStatus);

        modelBuilder.Entity<Bike>()
            .HasIndex(b => b.BikeTypeId);

        modelBuilder.Entity<Bike>()
            .HasIndex(b => b.AvailabilityStatus);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.BookingStatus);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.StartDate);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });

        // Add indexes for soft delete fields
        modelBuilder.Entity<User>()
            .HasIndex(u => u.IsDeleted);

        modelBuilder.Entity<Bike>()
            .HasIndex(b => b.IsDeleted);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.IsDeleted);

        // Add performance indexes (non-conflicting only)
        modelBuilder.Entity<User>()
            .HasIndex(u => u.IsEmailVerified);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.LastLoginAt);

        modelBuilder.Entity<Bike>()
            .HasIndex(b => b.ViewCount);

        modelBuilder.Entity<Bike>()
            .HasIndex(b => b.BookingCount);

        modelBuilder.Entity<Bike>()
            .HasIndex(b => new { b.HourlyRate, b.DailyRate });

        modelBuilder.Entity<Rating>()
            .HasIndex(r => r.RatingValue);

        modelBuilder.Entity<Rating>()
            .HasIndex(r => r.IsFlagged);

        // Global query filters for soft delete
        modelBuilder.Entity<User>()
            .HasQueryFilter(u => !u.IsDeleted);

        modelBuilder.Entity<Bike>()
            .HasQueryFilter(b => !b.IsDeleted);

        modelBuilder.Entity<Booking>()
            .HasQueryFilter(b => !b.IsDeleted);
    }
}

