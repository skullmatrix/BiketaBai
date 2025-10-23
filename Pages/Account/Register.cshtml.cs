using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly WalletService _walletService;
    private readonly PointsService _pointsService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;

    public RegisterModel(BiketaBaiDbContext context, WalletService walletService, PointsService pointsService, EmailService emailService, IConfiguration configuration)
    {
        _context = context;
        _walletService = walletService;
        _pointsService = pointsService;
        _emailService = emailService;
        _configuration = configuration;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Phone]
        public string? Phone { get; set; }

        public string? Address { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Register as Renter")]
        public bool IsRenter { get; set; }

        [Display(Name = "Register as Owner")]
        public bool IsOwner { get; set; }
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

        // Validate user type selection
        if (!Input.IsRenter && !Input.IsOwner)
        {
            ErrorMessage = "Please select at least one user type (Renter or Owner)";
            return Page();
        }

        // Validate password strength
        if (!PasswordHelper.IsPasswordStrong(Input.Password))
        {
            ErrorMessage = "Password must be at least 8 characters with uppercase, lowercase, and number";
            return Page();
        }

        // Check if email already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == Input.Email);
        if (existingUser != null)
        {
            ErrorMessage = "Email address is already registered";
            return Page();
        }

        // Generate verification token
        var verificationToken = Guid.NewGuid().ToString("N");
        
        // Create user (NOT verified yet)
        var user = new User
        {
            FullName = Input.FullName,
            Email = Input.Email,
            Phone = Input.Phone,
            Address = Input.Address,
            PasswordHash = PasswordHelper.HashPassword(Input.Password),
            IsRenter = Input.IsRenter,
            IsOwner = Input.IsOwner,
            IsAdmin = false,
            IsEmailVerified = false, // Not verified yet
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24), // 24 hour expiry
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Create wallet and points for new user
        await _walletService.GetOrCreateWalletAsync(user.UserId);
        await _pointsService.GetOrCreatePointsAsync(user.UserId);

        // Send verification email
        try
        {
            var verificationLink = $"{Request.Scheme}://{Request.Host}/Account/VerifyEmail?token={verificationToken}&email={Uri.EscapeDataString(user.Email)}";
            await _emailService.SendVerificationEmailAsync(user.Email, user.FullName, verificationLink);
            
            TempData["SuccessMessage"] = "Registration successful! Please check your email to verify your account.";
        }
        catch (Exception ex)
        {
            // Log error but don't fail registration
            TempData["ErrorMessage"] = "Account created but failed to send verification email. Please contact support.";
        }

        return RedirectToPage("/Account/RegistrationSuccess");
    }
}

