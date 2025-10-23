using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("availability_statuses")]
public class AvailabilityStatus
{
    [Key]
    [Column("status_id")]
    public int StatusId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status_name")]
    public string StatusName { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<Bike> Bikes { get; set; } = new List<Bike>();
}

