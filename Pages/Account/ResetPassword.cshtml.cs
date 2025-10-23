using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Helpers;
using BiketaBai.Services;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly EmailService _emailService;

    public ResetPasswordModel(BiketaBaiDbContext context, EmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool IsTokenValid { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string? token, string? email)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
        {
            IsTokenValid = false;
            ErrorMessage = "Invalid password reset link. Token or email is missing.";
            return Page();
        }

        // Validate token
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.PasswordResetToken == token);

        if (user == null)
        {
            IsTokenValid = false;
            ErrorMessage = "Invalid password reset link. User not found or token doesn't match.";
            return Page();
        }

        if (user.PasswordResetTokenExpires == null || user.PasswordResetTokenExpires < DateTime.UtcNow)
        {
            IsTokenValid = false;
            ErrorMessage = "This password reset link has expired. Please request a new one.";
            return Page();
        }

        // Token is valid
        IsTokenValid = true;
        Input.Token = token;
        Input.Email = email;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            IsTokenValid = true;
            return Page();
        }

        // Validate password strength
        if (!PasswordHelper.IsPasswordStrong(Input.NewPassword))
        {
            IsTokenValid = true;
            ErrorMessage = "Password must be at least 8 characters with uppercase, lowercase, and number";
            return Page();
        }

        // Find user
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == Input.Email && u.PasswordResetToken == Input.Token);

        if (user == null)
        {
            IsTokenValid = false;
            ErrorMessage = "Invalid password reset request.";
            return Page();
        }

        // Check token expiration again
        if (user.PasswordResetTokenExpires == null || user.PasswordResetTokenExpires < DateTime.UtcNow)
        {
            IsTokenValid = false;
            ErrorMessage = "This password reset link has expired. Please request a new one.";
            return Page();
        }

        // Update password
        user.PasswordHash = PasswordHelper.HashPassword(Input.NewPassword);
        user.PasswordResetToken = null; // Clear the token
        user.PasswordResetTokenExpires = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Send confirmation email
        try
        {
            await _emailService.SendPasswordChangedNotificationAsync(user.Email, user.FullName);
        }
        catch
        {
            // Don't fail if confirmation email fails
        }

        TempData["SuccessMessage"] = "Your password has been reset successfully! You can now login with your new password.";
        return RedirectToPage("/Account/Login");
    }
}

