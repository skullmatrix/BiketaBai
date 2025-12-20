using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("bike_damages")]
public class BikeDamage
{
    [Key]
    [Column("damage_id")]
    public int DamageId { get; set; }

    [Required]
    [Column("booking_id")]
    public int BookingId { get; set; }

    [Required]
    [Column("bike_id")]
    public int BikeId { get; set; }

    [Required]
    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Required]
    [Column("renter_id")]
    public int RenterId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("damage_description")]
    public string DamageDescription { get; set; } = string.Empty;

    [Column("damage_details", TypeName = "text")]
    public string? DamageDetails { get; set; }

    [Required]
    [Column("damage_cost", TypeName = "decimal(10,2)")]
    public decimal DamageCost { get; set; }

    [MaxLength(255)]
    [Column("damage_image_url")]
    public string? DamageImageUrl { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("damage_status")]
    public string DamageStatus { get; set; } = "Pending"; // Pending, Paid, Disputed, Waived

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [MaxLength(500)]
    [Column("payment_notes")]
    public string? PaymentNotes { get; set; }

    // Navigation properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;

    [ForeignKey("BikeId")]
    public virtual Bike Bike { get; set; } = null!;

    [ForeignKey("OwnerId")]
    public virtual User Owner { get; set; } = null!;

    [ForeignKey("RenterId")]
    public virtual User Renter { get; set; } = null!;
}

