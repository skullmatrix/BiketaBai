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

    [Column("quantity")]
    public int Quantity { get; set; } = 1; // Number of bikes rented in this booking

    [Column("base_rate")]
    public decimal BaseRate { get; set; }

    [Column("service_fee")]
    public decimal ServiceFee { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("booking_status")]
    public string BookingStatus { get; set; } = "Pending"; // Pending, Active, Completed, Cancelled

    [Column("distance_saved_km")]
    public decimal? DistanceSavedKm { get; set; }

    [MaxLength(255)]
    [Column("pickup_location")]
    public string? PickupLocation { get; set; }

    [MaxLength(255)]
    [Column("return_location")]
    public string? ReturnLocation { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("owner_confirmed_at")]
    public DateTime? OwnerConfirmedAt { get; set; }

    [Column("renter_confirmed_pickup_at")]
    public DateTime? RenterConfirmedPickupAt { get; set; }

    [Column("renter_confirmed_return_at")]
    public DateTime? RenterConfirmedReturnAt { get; set; }

    [MaxLength(500)]
    [Column("special_instructions")]
    public string? SpecialInstructions { get; set; }

    [Column("location_permission_granted")]
    public bool LocationPermissionGranted { get; set; } = false;

    [Column("location_permission_denied_at")]
    public DateTime? LocationPermissionDeniedAt { get; set; }

    [Column("is_reported_lost")]
    public bool IsReportedLost { get; set; } = false;

    [Column("reported_lost_at")]
    public DateTime? ReportedLostAt { get; set; }

    [MaxLength(500)]
    [Column("lost_report_notes")]
    public string? LostReportNotes { get; set; }

    // Navigation properties
    [ForeignKey("RenterId")]
    public virtual User Renter { get; set; } = null!;

    [ForeignKey("BikeId")]
    public virtual Bike Bike { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public virtual ICollection<LocationTracking> LocationTracking { get; set; } = new List<LocationTracking>();
}

