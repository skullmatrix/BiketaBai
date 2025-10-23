using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("credit_transactions")]
public class CreditTransaction
{
    [Key]
    [Column("transaction_id")]
    public int TransactionId { get; set; }

    [Required]
    [Column("wallet_id")]
    public int WalletId { get; set; }

    [Required]
    [Column("transaction_type_id")]
    public int TransactionTypeId { get; set; }

    [Required]
    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("balance_before")]
    public decimal BalanceBefore { get; set; }

    [Column("balance_after")]
    public decimal BalanceAfter { get; set; }

    [MaxLength(255)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(100)]
    [Column("reference_id")]
    public string? ReferenceId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("WalletId")]
    public virtual Wallet Wallet { get; set; } = null!;

    [ForeignKey("TransactionTypeId")]
    public virtual TransactionType TransactionType { get; set; } = null!;
}

