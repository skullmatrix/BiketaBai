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
    public DbSet<AvailabilityStatus> AvailabilityStatuses { get; set; }
    public DbSet<Bike> Bikes { get; set; }
    public DbSet<BikeImage> BikeImages { get; set; }
    public DbSet<BookingStatus> BookingStatuses { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<PaymentMethod> PaymentMethods { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Wallet> Wallets { get; set; }
    public DbSet<TransactionType> TransactionTypes { get; set; }
    public DbSet<CreditTransaction> CreditTransactions { get; set; }
    public DbSet<Rating> Ratings { get; set; }
    public DbSet<Points> Points { get; set; }
    public DbSet<PointsHistory> PointsHistory { get; set; }
    public DbSet<Notification> Notifications { get; set; }

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

        modelBuilder.Entity<Bike>()
            .Property(b => b.Mileage)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Bike>()
            .Property(b => b.Latitude)
            .HasPrecision(10, 7);

        modelBuilder.Entity<Bike>()
            .Property(b => b.Longitude)
            .HasPrecision(10, 7);

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

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.RefundAmount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Wallet>()
            .Property(w => w.Balance)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CreditTransaction>()
            .Property(ct => ct.Amount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CreditTransaction>()
            .Property(ct => ct.BalanceBefore)
            .HasPrecision(10, 2);

        modelBuilder.Entity<CreditTransaction>()
            .Property(ct => ct.BalanceAfter)
            .HasPrecision(10, 2);

        // Configure relationships
        modelBuilder.Entity<User>()
            .HasOne(u => u.Wallet)
            .WithOne(w => w.User)
            .HasForeignKey<Wallet>(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Points)
            .WithOne(p => p.User)
            .HasForeignKey<Points>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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

        // Seed data for lookup tables
        modelBuilder.Entity<BikeType>().HasData(
            new BikeType { BikeTypeId = 1, TypeName = "Mountain Bike", Description = "Off-road cycling" },
            new BikeType { BikeTypeId = 2, TypeName = "Road Bike", Description = "Paved road cycling" },
            new BikeType { BikeTypeId = 3, TypeName = "Hybrid Bike", Description = "Versatile for various terrains" },
            new BikeType { BikeTypeId = 4, TypeName = "Electric Bike", Description = "E-bike with motor assistance" },
            new BikeType { BikeTypeId = 5, TypeName = "City/Commuter Bike", Description = "Urban commuting" },
            new BikeType { BikeTypeId = 6, TypeName = "BMX", Description = "Tricks and stunts" },
            new BikeType { BikeTypeId = 7, TypeName = "Folding Bike", Description = "Compact and portable" }
        );

        modelBuilder.Entity<AvailabilityStatus>().HasData(
            new AvailabilityStatus { StatusId = 1, StatusName = "Available" },
            new AvailabilityStatus { StatusId = 2, StatusName = "Rented" },
            new AvailabilityStatus { StatusId = 3, StatusName = "Maintenance" },
            new AvailabilityStatus { StatusId = 4, StatusName = "Inactive" }
        );

        modelBuilder.Entity<BookingStatus>().HasData(
            new BookingStatus { StatusId = 1, StatusName = "Pending" },
            new BookingStatus { StatusId = 2, StatusName = "Active" },
            new BookingStatus { StatusId = 3, StatusName = "Completed" },
            new BookingStatus { StatusId = 4, StatusName = "Cancelled" }
        );

        modelBuilder.Entity<PaymentMethod>().HasData(
            new PaymentMethod { MethodId = 1, MethodName = "Wallet" },
            new PaymentMethod { MethodId = 2, MethodName = "GCash" },
            new PaymentMethod { MethodId = 3, MethodName = "QRPH" },
            new PaymentMethod { MethodId = 4, MethodName = "Cash" }
        );

        modelBuilder.Entity<TransactionType>().HasData(
            new TransactionType { TypeId = 1, TypeName = "Load" },
            new TransactionType { TypeId = 2, TypeName = "Withdrawal" },
            new TransactionType { TypeId = 3, TypeName = "Rental Payment" },
            new TransactionType { TypeId = 4, TypeName = "Rental Earnings" },
            new TransactionType { TypeId = 5, TypeName = "Refund" },
            new TransactionType { TypeId = 6, TypeName = "Service Fee" }
        );

        // Create indexes for better performance
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Bike>()
            .HasIndex(b => b.AvailabilityStatusId);

        modelBuilder.Entity<Bike>()
            .HasIndex(b => b.BikeTypeId);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.BookingStatusId);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.StartDate);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });
    }
}

