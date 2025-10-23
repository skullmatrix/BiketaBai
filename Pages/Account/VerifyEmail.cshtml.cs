using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Services;

namespace BiketaBai.Pages.Account;

public class VerifyEmailModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly EmailService _emailService;
    private readonly PointsService _pointsService;
    private readonly IConfiguration _configuration;

    public VerifyEmailModel(BiketaBaiDbContext context, EmailService emailService, PointsService pointsService, IConfiguration configuration)
    {
        _context = context;
        _emailService = emailService;
        _pointsService = pointsService;
        _configuration = configuration;
    }

    public bool IsVerified { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? token, string? email)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
        {
            IsVerified = false;
            ErrorMessage = "Invalid verification link. Token or email is missing.";
            return Page();
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.EmailVerificationToken == token);

        if (user == null)
        {
            IsVerified = false;
            ErrorMessage = "Invalid verification link. User not found or token doesn't match.";
            return Page();
        }

        if (user.IsEmailVerified)
        {
            IsVerified = true;
            return Page(); // Already verified, show success
        }

        if (user.EmailVerificationTokenExpires == null || user.EmailVerificationTokenExpires < DateTime.UtcNow)
        {
            IsVerified = false;
            ErrorMessage = "Verification link has expired. Please register again.";
            return Page();
        }

        // Verify the email
        user.IsEmailVerified = true;
        user.EmailVerificationToken = null; // Clear the token
        user.EmailVerificationTokenExpires = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Award complete profile points
        var completeProfilePoints = _configuration.GetValue<int>("PointsRules:CompleteProfile");
        await _pointsService.AwardPointsAsync(user.UserId, completeProfilePoints, "Email verified and profile completed", "EmailVerification");

        // Send welcome email
        try
        {
            await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);
        }
        catch
        {
            // Don't fail verification if welcome email fails
        }

        IsVerified = true;
        return Page();
    }
}

