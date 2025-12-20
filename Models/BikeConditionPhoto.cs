using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("bike_condition_photos")]
public class BikeConditionPhoto
{
    [Key]
    [Column("condition_photo_id")]
    public int ConditionPhotoId { get; set; }

    [Required]
    [Column("booking_id")]
    public int BookingId { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("photo_url")]
    public string PhotoUrl { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("photo_description")]
    public string? PhotoDescription { get; set; }

    [Required]
    [Column("taken_at")]
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("taken_by_renter")]
    public bool TakenByRenter { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;
}

