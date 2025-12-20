using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("bikes")]
public class Bike
{
    [Key]
    [Column("bike_id")]
    public int BikeId { get; set; }

    [Required]
    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Column("store_id")]
    public int? StoreId { get; set; } // Optional: link to store if normalized

    [Required]
    [Column("bike_type_id")]
    public int BikeTypeId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("brand")]
    public string Brand { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("model")]
    public string Model { get; set; } = string.Empty;

    [Column("view_count")]
    public int ViewCount { get; set; } = 0;

    [Column("booking_count")]
    public int BookingCount { get; set; } = 0;

    [Column("description", TypeName = "text")]
    public string? Description { get; set; }

    [Column("hourly_rate")]
    public decimal HourlyRate { get; set; }

    [Column("daily_rate")]
    public decimal DailyRate { get; set; }

    [Column("quantity")]
    public int Quantity { get; set; } = 1; // Number of bikes available for this listing

    [Required]
    [MaxLength(50)]
    [Column("availability_status")]
    public string AvailabilityStatus { get; set; } = "Available"; // Available, Rented, Maintenance, Inactive

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [MaxLength(100)]
    [Column("deleted_by")]
    public string? DeletedBy { get; set; }

    // Navigation properties
    [ForeignKey("OwnerId")]
    public virtual User Owner { get; set; } = null!;

    [ForeignKey("BikeTypeId")]
    public virtual BikeType BikeType { get; set; } = null!;

    [ForeignKey("StoreId")]
    public virtual Store? Store { get; set; }

    public virtual ICollection<BikeImage> BikeImages { get; set; } = new List<BikeImage>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public virtual ICollection<BikeDamage> BikeDamages { get; set; } = new List<BikeDamage>();
}

