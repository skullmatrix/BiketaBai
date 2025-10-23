using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Services;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly EmailService _emailService;

    public ForgotPasswordModel(BiketaBaiDbContext context, EmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Find user by email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == Input.Email);

        // For security, always show success message even if email doesn't exist
        // This prevents email enumeration attacks
        if (user == null)
        {
            SuccessMessage = "If an account exists with that email, you will receive a password reset link shortly.";
            return Page();
        }

        // Check if email is verified - only verified users can reset password
        if (!user.IsEmailVerified)
        {
            SuccessMessage = "If an account exists with that email, you will receive a password reset link shortly.";
            return Page();
        }

        // Generate reset token (valid for 1 hour)
        var resetToken = Guid.NewGuid().ToString("N");
        user.PasswordResetToken = resetToken;
        user.PasswordResetTokenExpires = DateTime.UtcNow.AddHours(1);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Send password reset email
        try
        {
            var resetLink = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?token={resetToken}&email={Uri.EscapeDataString(user.Email)}";
            await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, resetLink);
            
            SuccessMessage = "If an account exists with that email, you will receive a password reset link shortly. Please check your inbox and spam folder.";
        }
        catch (Exception ex)
        {
            // Log error but don't reveal it to user for security
            Console.WriteLine($"Failed to send password reset email: {ex.Message}");
            SuccessMessage = "If an account exists with that email, you will receive a password reset link shortly.";
        }

        return Page();
    }
}

