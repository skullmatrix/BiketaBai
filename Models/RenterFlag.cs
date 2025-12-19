using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("renter_flags")]
public class RenterFlag
{
    [Key]
    [Column("flag_id")]
    public int FlagId { get; set; }

    [Required]
    [Column("booking_id")]
    public int BookingId { get; set; }

    [Required]
    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Required]
    [Column("renter_id")]
    public int RenterId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("flag_reason")]
    public string FlagReason { get; set; } = string.Empty; // Damage, LateReturn, Misconduct, Other

    [Column("flag_description", TypeName = "text")]
    public string? FlagDescription { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_resolved")]
    public bool IsResolved { get; set; } = false;

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    // Navigation properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;

    [ForeignKey("OwnerId")]
    public virtual User Owner { get; set; } = null!;

    [ForeignKey("RenterId")]
    public virtual User Renter { get; set; } = null!;
}


