using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("booking_statuses")]
public class BookingStatus
{
    [Key]
    [Column("status_id")]
    public int StatusId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status_name")]
    public string StatusName { get; set; } = string.Empty;

    // Navigation properties
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

