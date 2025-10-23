using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("users")]
public class User
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [EmailAddress]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(255)]
    [Column("address")]
    public string? Address { get; set; }

    [Column("is_renter")]
    public bool IsRenter { get; set; } = false;

    [Column("is_owner")]
    public bool IsOwner { get; set; } = false;

    [Column("is_admin")]
    public bool IsAdmin { get; set; } = false;

    [MaxLength(255)]
    [Column("profile_photo_url")]
    public string? ProfilePhotoUrl { get; set; }

    [Column("is_email_verified")]
    public bool IsEmailVerified { get; set; } = false;

    [MaxLength(100)]
    [Column("email_verification_token")]
    public string? EmailVerificationToken { get; set; }

    [Column("email_verification_token_expires")]
    public DateTime? EmailVerificationTokenExpires { get; set; }

    [Column("is_suspended")]
    public bool IsSuspended { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<Bike> Bikes { get; set; } = new List<Bike>();
    public virtual ICollection<Booking> BookingsAsRenter { get; set; } = new List<Booking>();
    public virtual ICollection<Rating> RatingsGiven { get; set; } = new List<Rating>();
    public virtual ICollection<Rating> RatingsReceived { get; set; } = new List<Rating>();
    public virtual Wallet? Wallet { get; set; }
    public virtual Points? Points { get; set; }
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

