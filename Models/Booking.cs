using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("bookings")]
public class Booking
{
    [Key]
    [Column("booking_id")]
    public int BookingId { get; set; }

    [Required]
    [Column("renter_id")]
    public int RenterId { get; set; }

    [Required]
    [Column("bike_id")]
    public int BikeId { get; set; }

    [Required]
    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Required]
    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("actual_return_date")]
    public DateTime? ActualReturnDate { get; set; }

    [Column("rental_hours")]
    public decimal RentalHours { get; set; }

    [Column("base_rate")]
    public decimal BaseRate { get; set; }

    [Column("service_fee")]
    public decimal ServiceFee { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Required]
    [Column("booking_status_id")]
    public int BookingStatusId { get; set; }

    [Column("distance_saved_km")]
    public decimal? DistanceSavedKm { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    // Navigation properties
    [ForeignKey("RenterId")]
    public virtual User Renter { get; set; } = null!;

    [ForeignKey("BikeId")]
    public virtual Bike Bike { get; set; } = null!;

    [ForeignKey("BookingStatusId")]
    public virtual BookingStatus BookingStatus { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
}

