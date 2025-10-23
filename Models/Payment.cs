using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("payments")]
public class Payment
{
    [Key]
    [Column("payment_id")]
    public int PaymentId { get; set; }

    [Required]
    [Column("booking_id")]
    public int BookingId { get; set; }

    [Required]
    [Column("payment_method_id")]
    public int PaymentMethodId { get; set; }

    [Required]
    [Column("amount")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded

    [MaxLength(100)]
    [Column("transaction_reference")]
    public string? TransactionReference { get; set; }

    [Column("payment_date")]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Column("refund_amount")]
    public decimal? RefundAmount { get; set; }

    [Column("refund_date")]
    public DateTime? RefundDate { get; set; }

    [MaxLength(500)]
    [Column("notes")]
    public string? Notes { get; set; }

    // Navigation properties
    [ForeignKey("BookingId")]
    public virtual Booking Booking { get; set; } = null!;

    [ForeignKey("PaymentMethodId")]
    public virtual PaymentMethod PaymentMethod { get; set; } = null!;
}

