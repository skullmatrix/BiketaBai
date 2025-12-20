using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("renter_red_tags")]
public class RenterRedTag
{
    [Key]
    [Column("red_tag_id")]
    public int RedTagId { get; set; }

    [Required]
    [Column("renter_id")]
    public int RenterId { get; set; }

    [Required]
    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Column("booking_id")]
    public int? BookingId { get; set; } // Optional: can be red-tagged without specific booking

    [Required]
    [MaxLength(50)]
    [Column("red_tag_reason")]
    public string RedTagReason { get; set; } = string.Empty; // Delinquent, UnpaidDamages, MultipleViolations, Fraud, Other

    [Column("red_tag_description", TypeName = "text")]
    public string? RedTagDescription { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(500)]
    [Column("resolution_notes")]
    public string? ResolutionNotes { get; set; }

    [Column("resolved_by")]
    public int? ResolvedBy { get; set; } // Admin user ID who resolved it

    // Navigation properties
    [ForeignKey("RenterId")]
    public virtual User Renter { get; set; } = null!;

    [ForeignKey("OwnerId")]
    public virtual User Owner { get; set; } = null!;

    [ForeignKey("BookingId")]
    public virtual Booking? Booking { get; set; }

    [ForeignKey("ResolvedBy")]
    public virtual User? Resolver { get; set; }
}

