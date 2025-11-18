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

    [MaxLength(255)]
    [Column("id_document_url")]
    public string? IdDocumentUrl { get; set; }

    [Column("is_verified_owner")]
    public bool IsVerifiedOwner { get; set; } = false;

    [Column("verification_date")]
    public DateTime? VerificationDate { get; set; }

    [MaxLength(100)]
    [Column("verification_status")]
    public string VerificationStatus { get; set; } = "Pending"; // Pending, Approved, Rejected

    [Column("is_email_verified")]
    public bool IsEmailVerified { get; set; } = false;

    [MaxLength(100)]
    [Column("email_verification_token")]
    public string? EmailVerificationToken { get; set; }

    [Column("email_verification_token_expires")]
    public DateTime? EmailVerificationTokenExpires { get; set; }

    [MaxLength(100)]
    [Column("password_reset_token")]
    public string? PasswordResetToken { get; set; }

    [Column("password_reset_token_expires")]
    public DateTime? PasswordResetTokenExpires { get; set; }

    [Column("is_suspended")]
    public bool IsSuspended { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [Column("login_count")]
    public int LoginCount { get; set; } = 0;

    // Store information (for owners)
    [MaxLength(255)]
    [Column("store_name")]
    public string? StoreName { get; set; }

    [MaxLength(500)]
    [Column("store_address")]
    public string? StoreAddress { get; set; }

    // ID and Address Verification
    [Column("id_verified")]
    public bool IdVerified { get; set; } = false;

    [Column("id_verified_at")]
    public DateTime? IdVerifiedAt { get; set; }

    [Column("address_verified")]
    public bool AddressVerified { get; set; } = false;

    [Column("address_verified_at")]
    public DateTime? AddressVerifiedAt { get; set; }

    [MaxLength(500)]
    [Column("id_extracted_address")]
    public string? IdExtractedAddress { get; set; }

    // Navigation properties
    public virtual ICollection<Bike> Bikes { get; set; } = new List<Bike>();
    public virtual ICollection<Booking> BookingsAsRenter { get; set; } = new List<Booking>();
    public virtual ICollection<Rating> RatingsGiven { get; set; } = new List<Rating>();
    public virtual ICollection<Rating> RatingsReceived { get; set; } = new List<Rating>();
    public virtual Wallet? Wallet { get; set; }
    public virtual Points? Points { get; set; }
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

