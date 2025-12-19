using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("stores")]
public class Store
{
    [Key]
    [Column("store_id")]
    public int StoreId { get; set; }

    [Required]
    [Column("owner_id")]
    public int OwnerId { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("store_name")]
    public string StoreName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("store_address")]
    public string StoreAddress { get; set; } = string.Empty;

    [Column("store_latitude")]
    public double? StoreLatitude { get; set; }

    [Column("store_longitude")]
    public double? StoreLongitude { get; set; }

    [Column("geofence_radius_km")]
    public decimal? GeofenceRadiusKm { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; } = true; // Primary store for the owner

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    [ForeignKey("OwnerId")]
    public virtual User Owner { get; set; } = null!;

    public virtual ICollection<Bike> Bikes { get; set; } = new List<Bike>();
}









