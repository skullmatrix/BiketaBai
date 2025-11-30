using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Services;
using BiketaBai.Helpers;
using Serilog;

namespace BiketaBai.Pages.Account;

public class VerifyPhoneModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly OtpService _otpService;

    public VerifyPhoneModel(BiketaBaiDbContext context, OtpService otpService)
    {
        _context = context;
        _otpService = otpService;
    }

    [BindProperty]
    public string? OtpCode { get; set; }

    public string? PhoneNumber { get; set; }
    public string? ReturnUrl { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (!User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Account/Login", new { returnUrl = "/Account/VerifyPhone" });

        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null || string.IsNullOrWhiteSpace(user.Phone))
        {
            TempData["ErrorMessage"] = "Please add your phone number in your profile first.";
            return RedirectToPage("/Account/Profile");
        }

        // Check if phone is already verified
        var hasVerifiedOtp = await _context.PhoneOtps
            .AnyAsync(o => o.PhoneNumber == user.Phone && o.IsVerified && o.VerifiedAt.HasValue);

        if (hasVerifiedOtp)
        {
            // Already verified, redirect to return URL or home
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToPage("/Index");
        }

        PhoneNumber = user.Phone;
        ReturnUrl = returnUrl;

        // Automatically send OTP when user arrives at the page
        // Check if there's already a valid OTP first
        var hasValidOtp = await _otpService.HasValidOtpAsync(user.Phone);
        
        if (!hasValidOtp)
        {
            // No valid OTP exists, send a new one automatically
            try
            {
                Log.Information("VerifyPhone: Auto-sending OTP to {PhoneNumber} for user {UserId}", user.Phone, userId.Value);
                var otpSent = await _otpService.GenerateAndSendOtpAsync(user.Phone);
                if (otpSent)
                {
                    SuccessMessage = "OTP code has been sent to your phone! Please check your SMS.";
                    Log.Information("VerifyPhone: OTP auto-sent successfully to {PhoneNumber}", user.Phone);
                }
                else
                {
                    ErrorMessage = "Failed to send OTP. Please try clicking 'Resend Code'.";
                    Log.Warning("VerifyPhone: Failed to auto-send OTP to {PhoneNumber}", user.Phone);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "VerifyPhone: Error auto-sending OTP to {PhoneNumber}", user.Phone);
                ErrorMessage = $"Error sending OTP: {ex.Message}. Please try clicking 'Resend Code'.";
            }
        }
        else
        {
            // Valid OTP already exists, inform user
            SuccessMessage = "An OTP code has already been sent to your phone. Please check your SMS.";
            Log.Information("VerifyPhone: Valid OTP already exists for {PhoneNumber}, skipping auto-send", user.Phone);
        }

        // Check for messages from TempData (these override auto-sent messages)
        if (TempData.ContainsKey("SuccessMessage"))
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
        if (TempData.ContainsKey("ErrorMessage"))
            ErrorMessage = TempData["ErrorMessage"]?.ToString();

        return Page();
    }

    public async Task<IActionResult> OnPostSendOtpAsync(string? returnUrl = null)
    {
        if (!User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Account/Login");

        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null || string.IsNullOrWhiteSpace(user.Phone))
        {
            TempData["ErrorMessage"] = "Please add your phone number in your profile first.";
            return RedirectToPage("/Account/Profile");
        }

        try
        {
            Log.Information("VerifyPhone: Attempting to send OTP to {PhoneNumber} for user {UserId}", user.Phone, userId.Value);
            var otpSent = await _otpService.GenerateAndSendOtpAsync(user.Phone);
            if (otpSent)
            {
                TempData["SuccessMessage"] = "OTP code sent to your phone! Please check your SMS.";
                Log.Information("VerifyPhone: OTP sent successfully to {PhoneNumber}", user.Phone);
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to send OTP. Please check your phone number and try again.";
                Log.Warning("VerifyPhone: Failed to send OTP to {PhoneNumber}", user.Phone);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "VerifyPhone: Error sending OTP to {PhoneNumber}", user.Phone);
            TempData["ErrorMessage"] = $"Error sending OTP: {ex.Message}. Please try again.";
        }

        return RedirectToPage("/Account/VerifyPhone", new { returnUrl });
    }

    public async Task<IActionResult> OnPostVerifyAsync(string? returnUrl = null)
    {
        if (!User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Account/Login");

        var userId = AuthHelper.GetCurrentUserId(User);
        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null || string.IsNullOrWhiteSpace(user.Phone))
        {
            TempData["ErrorMessage"] = "Please add your phone number in your profile first.";
            return RedirectToPage("/Account/Profile");
        }

        if (string.IsNullOrWhiteSpace(OtpCode))
        {
            TempData["ErrorMessage"] = "Please enter the OTP code.";
            return RedirectToPage("/Account/VerifyPhone", new { returnUrl });
        }

        try
        {
            var isValid = await _otpService.VerifyOtpAsync(user.Phone, OtpCode);
            if (isValid)
            {
                Log.Information("VerifyPhone: OTP verified successfully for user {UserId}, phone {PhoneNumber}", userId.Value, user.Phone);
                TempData["SuccessMessage"] = "Phone number verified successfully!";
                
                // Restore booking details from TempData if they exist
                if (TempData.ContainsKey("BookingQuantity"))
                    TempData.Keep("BookingQuantity");
                if (TempData.ContainsKey("BookingHours"))
                    TempData.Keep("BookingHours");
                if (TempData.ContainsKey("BikeId"))
                    TempData.Keep("BikeId");
                
                // Redirect to return URL or home
                if (!string.IsNullOrEmpty(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToPage("/Index");
            }
            else
            {
                TempData["ErrorMessage"] = "Invalid or expired OTP code. Please try again.";
                Log.Warning("VerifyPhone: Invalid OTP code for user {UserId}, phone {PhoneNumber}", userId.Value, user.Phone);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "VerifyPhone: Error verifying OTP for user {UserId}", userId.Value);
            TempData["ErrorMessage"] = $"Error verifying OTP: {ex.Message}. Please try again.";
        }

        return RedirectToPage("/Account/VerifyPhone", new { returnUrl });
    }
}

