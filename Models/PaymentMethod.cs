using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("payment_methods")]
public class PaymentMethod
{
    [Key]
    [Column("method_id")]
    public int MethodId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("method_name")]
    public string MethodName { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

