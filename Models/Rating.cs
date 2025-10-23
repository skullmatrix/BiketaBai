using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("ratings")]
public class Rating
{
    [Key]
    [Column("rating_id")]
    public int RatingId { get; set; }

    [Required]
    [Column("booking_id")]
    public int BookingId { get; set; }

    [Column("bike_id")]
    public int? BikeId { get; set; }

    [Required]
    [Column("rater_id")]
    public int RaterId { get; set; }

    [Required]
    [Column("rated_user_id")]
    public int RatedUserId { get; set; }

    [Required]
    [Range(1, 5)]
    [Column("rating_value")]
    public int RatingValue { get; set; }

    [Column("review", TypeName = "text")]
    public string? Review { get; set; }

    [MaxLength(50)]
    [Column("rating_category")]
    public string? RatingCategory { get; set; } // BikeCondition, OwnerCommunication, ValueForMoney, Responsibility, etc.

    [Column("is_renter_rating_owner")]
    public bool IsRenterRatingOwner { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_flagged")]
    public bool IsFlagged { get; set; } = false;

    // Navigation properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;

    [ForeignKey("BikeId")]
    public virtual Bike? Bike { get; set; }

    [ForeignKey("RaterId")]
    public virtual User Rater { get; set; } = null!;

    [ForeignKey("RatedUserId")]
    public virtual User RatedUser { get; set; } = null!;
}

