using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("location_tracking")]
public class LocationTracking
{
    [Key]
    [Column("tracking_id")]
    public int TrackingId { get; set; }

    [Required]
    [Column("booking_id")]
    public int BookingId { get; set; }

    [Required]
    [Column("latitude")]
    public double Latitude { get; set; }

    [Required]
    [Column("longitude")]
    public double Longitude { get; set; }

    [Column("distance_from_store_km")]
    public decimal? DistanceFromStoreKm { get; set; }

    [Column("is_within_geofence")]
    public bool IsWithinGeofence { get; set; } = true;

    [Column("tracked_at")]
    public DateTime TrackedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}

