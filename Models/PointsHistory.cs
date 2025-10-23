using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("points_history")]
public class PointsHistory
{
    [Key]
    [Column("history_id")]
    public int HistoryId { get; set; }

    [Required]
    [Column("points_id")]
    public int PointsId { get; set; }

    [Required]
    [Column("points_change")]
    public int PointsChange { get; set; }

    [Column("points_before")]
    public int PointsBefore { get; set; }

    [Column("points_after")]
    public int PointsAfter { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("reference_id")]
    public string? ReferenceId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("PointsId")]
    public virtual Points Points { get; set; } = null!;
}

