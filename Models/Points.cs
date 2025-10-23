using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("points")]
public class Points
{
    [Key]
    [Column("points_id")]
    public int PointsId { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("total_points")]
    public int TotalPoints { get; set; } = 0;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;

    public virtual ICollection<PointsHistory> PointsHistory { get; set; } = new List<PointsHistory>();
}

