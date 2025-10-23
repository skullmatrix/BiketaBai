using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("transaction_types")]
public class TransactionType
{
    [Key]
    [Column("type_id")]
    public int TypeId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("type_name")]
    public string TypeName { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<CreditTransaction> CreditTransactions { get; set; } = new List<CreditTransaction>();
}

