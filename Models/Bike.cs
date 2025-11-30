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
    [Column("availability_status_id")]
    public int AvailabilityStatusId { get; set; } = 1; // Default to "Available"

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

    [ForeignKey("AvailabilityStatusId")]
    public virtual AvailabilityStatus AvailabilityStatus { get; set; } = null!;

    public virtual ICollection<BikeImage> BikeImages { get; set; } = new List<BikeImage>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}

