using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace BiketaBai.Services;

public class OtpService
{
    private readonly BiketaBaiDbContext _context;
    private readonly SmsService _smsService;

    public OtpService(BiketaBaiDbContext context, SmsService smsService)
    {
        _context = context;
        _smsService = smsService;
    }

    /// <summary>
    /// Generates and sends an OTP code to the specified phone number
    /// </summary>
    /// <param name="phoneNumber">Phone number in local or international format</param>
    /// <returns>True if OTP was generated and sent successfully</returns>
    public async Task<bool> GenerateAndSendOtpAsync(string phoneNumber)
    {
        try
        {
            // Generate 6-digit OTP code
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            // Set expiration to 10 minutes from now
            var expiresAt = DateTime.UtcNow.AddMinutes(10);

            // Invalidate any existing unverified OTPs for this phone number
            var existingOtps = await _context.PhoneOtps
                .Where(o => o.PhoneNumber == phoneNumber && !o.IsVerified && o.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var existingOtp in existingOtps)
            {
                existingOtp.IsVerified = true; // Mark as used/invalid
            }

            // Create new OTP record
            var phoneOtp = new PhoneOtp
            {
                PhoneNumber = phoneNumber,
                OtpCode = otpCode,
                ExpiresAt = expiresAt,
                IsVerified = false,
                CreatedAt = DateTime.UtcNow,
                Attempts = 0,
                MaxAttempts = 5
            };

            _context.PhoneOtps.Add(phoneOtp);
            await _context.SaveChangesAsync();

            // Send OTP via SMS
            var smsSent = await _smsService.SendOtpAsync(phoneNumber, otpCode);

            if (smsSent)
            {
                Log.Information("OTP generated and sent successfully to {PhoneNumber}", phoneNumber);
                return true;
            }
            else
            {
                Log.Warning("OTP generated but SMS sending failed for {PhoneNumber}", phoneNumber);
                // OTP is still saved in database, but SMS failed
                // You might want to delete it or handle this case differently
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error generating OTP for {PhoneNumber}. Error: {ErrorMessage}", 
                phoneNumber, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Verifies an OTP code for a phone number
    /// </summary>
    /// <param name="phoneNumber">Phone number in local or international format</param>
    /// <param name="otpCode">6-digit OTP code to verify</param>
    /// <returns>True if OTP is valid and verified, false otherwise</returns>
    public async Task<bool> VerifyOtpAsync(string phoneNumber, string otpCode)
    {
        try
        {
            // Find the most recent unverified OTP for this phone number
            var phoneOtp = await _context.PhoneOtps
                .Where(o => o.PhoneNumber == phoneNumber && !o.IsVerified)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (phoneOtp == null)
            {
                Log.Warning("No OTP found for phone number {PhoneNumber}", phoneNumber);
                return false;
            }

            // Check if OTP has expired
            if (phoneOtp.ExpiresAt < DateTime.UtcNow)
            {
                Log.Warning("OTP expired for phone number {PhoneNumber}. Expired at: {ExpiresAt}", 
                    phoneNumber, phoneOtp.ExpiresAt);
                return false;
            }

            // Check if max attempts exceeded
            if (phoneOtp.Attempts >= phoneOtp.MaxAttempts)
            {
                Log.Warning("Max OTP verification attempts exceeded for phone number {PhoneNumber}", phoneNumber);
                phoneOtp.IsVerified = true; // Mark as used to prevent further attempts
                await _context.SaveChangesAsync();
                return false;
            }

            // Increment attempts
            phoneOtp.Attempts++;

            // Verify OTP code
            if (phoneOtp.OtpCode == otpCode)
            {
                phoneOtp.IsVerified = true;
                phoneOtp.VerifiedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                Log.Information("OTP verified successfully for phone number {PhoneNumber}", phoneNumber);
                return true;
            }
            else
            {
                await _context.SaveChangesAsync();
                Log.Warning("Invalid OTP code for phone number {PhoneNumber}. Attempts: {Attempts}/{MaxAttempts}", 
                    phoneNumber, phoneOtp.Attempts, phoneOtp.MaxAttempts);
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error verifying OTP for {PhoneNumber}. Error: {ErrorMessage}", 
                phoneNumber, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Checks if a phone number has a valid (unexpired, unverified) OTP
    /// </summary>
    /// <param name="phoneNumber">Phone number to check</param>
    /// <returns>True if a valid OTP exists</returns>
    public async Task<bool> HasValidOtpAsync(string phoneNumber)
    {
        try
        {
            var hasValidOtp = await _context.PhoneOtps
                .AnyAsync(o => o.PhoneNumber == phoneNumber 
                    && !o.IsVerified 
                    && o.ExpiresAt > DateTime.UtcNow
                    && o.Attempts < o.MaxAttempts);

            return hasValidOtp;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking valid OTP for {PhoneNumber}. Error: {ErrorMessage}", 
                phoneNumber, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Cleans up expired OTPs (older than 24 hours)
    /// </summary>
    public async Task CleanupExpiredOtpsAsync()
    {
        try
        {
            var expiredOtps = await _context.PhoneOtps
                .Where(o => o.ExpiresAt < DateTime.UtcNow.AddHours(-24))
                .ToListAsync();

            if (expiredOtps.Any())
            {
                _context.PhoneOtps.RemoveRange(expiredOtps);
                await _context.SaveChangesAsync();
                Log.Information("Cleaned up {Count} expired OTP records", expiredOtps.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error cleaning up expired OTPs. Error: {ErrorMessage}", ex.Message);
        }
    }
}

