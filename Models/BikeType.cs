using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("bike_types")]
public class BikeType
{
    [Key]
    [Column("bike_type_id")]
    public int BikeTypeId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("type_name")]
    public string TypeName { get; set; } = string.Empty;

    [MaxLength(255)]
    [Column("description")]
    public string? Description { get; set; }

    // Navigation properties
    public virtual ICollection<Bike> Bikes { get; set; } = new List<Bike>();
}

