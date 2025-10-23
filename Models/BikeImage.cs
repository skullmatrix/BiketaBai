using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("bike_images")]
public class BikeImage
{
    [Key]
    [Column("image_id")]
    public int ImageId { get; set; }

    [Required]
    [Column("bike_id")]
    public int BikeId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("image_url")]
    public string ImageUrl { get; set; } = string.Empty;

    [Column("is_primary")]
    public bool IsPrimary { get; set; } = false;

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("BikeId")]
    public virtual Bike Bike { get; set; } = null!;
}

