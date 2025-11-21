using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BiketaBai.Models;

[Table("phone_otps")]
public class PhoneOtp
{
    [Key]
    [Column("otp_id")]
    public int OtpId { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(6)]
    [Column("otp_code")]
    public string OtpCode { get; set; } = string.Empty;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("is_verified")]
    public bool IsVerified { get; set; } = false;

    [Column("verified_at")]
    public DateTime? VerifiedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("attempts")]
    public int Attempts { get; set; } = 0;

    [Column("max_attempts")]
    public int MaxAttempts { get; set; } = 5;
}

