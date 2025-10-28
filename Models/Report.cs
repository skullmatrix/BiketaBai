using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("reports")]
public class Report
{
    [Key]
    [Column("report_id")]
    public int ReportId { get; set; }

    [Required]
    [Column("reporter_id")]
    public int ReporterId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("report_type")]
    public string ReportType { get; set; } = string.Empty; // "Bike Issue", "Payment", "User Misconduct", "Other"

    [Required]
    [MaxLength(255)]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [Column("description", TypeName = "TEXT")]
    public string Description { get; set; } = string.Empty;

    [Column("reported_user_id")]
    public int? ReportedUserId { get; set; }

    [Column("reported_bike_id")]
    public int? ReportedBikeId { get; set; }

    [Column("booking_id")]
    public int? BookingId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = "Pending"; // Pending, Assigned, In Progress, Resolved, Closed

    [Column("assigned_to")]
    public int? AssignedToAdminId { get; set; }

    [MaxLength(50)]
    [Column("priority")]
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical

    [Column("admin_notes", TypeName = "TEXT")]
    public string? AdminNotes { get; set; }

    [Column("resolution", TypeName = "TEXT")]
    public string? Resolution { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    // Navigation properties
    [ForeignKey("ReporterId")]
    public virtual User Reporter { get; set; } = null!;

    [ForeignKey("ReportedUserId")]
    public virtual User? ReportedUser { get; set; }

    [ForeignKey("ReportedBikeId")]
    public virtual Bike? ReportedBike { get; set; }

    [ForeignKey("BookingId")]
    public virtual Booking? Booking { get; set; }

    [ForeignKey("AssignedToAdminId")]
    public virtual User? AssignedAdmin { get; set; }
}

