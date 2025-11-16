using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Helpers;
using BiketaBai.Services;
using System.ComponentModel.DataAnnotations;
using Serilog;

namespace BiketaBai.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly WalletService _walletService;
    private readonly PointsService _pointsService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public RegisterModel(BiketaBaiDbContext context, WalletService walletService, PointsService pointsService, EmailService emailService, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _context = context;
        _walletService = walletService;
        _pointsService = pointsService;
        _emailService = emailService;
        _configuration = configuration;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? IdDocument { get; set; }

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

        // Validate ID document if registering as owner
        if (Input.IsOwner && IdDocument == null)
        {
            ErrorMessage = "ID document is required for bike owners";
            return Page();
        }

        string? idDocumentPath = null;

        // Handle ID document upload for owners
        if (Input.IsOwner && IdDocument != null)
        {
            // Validate file size (max 5MB)
            if (IdDocument.Length > 5 * 1024 * 1024)
            {
                ErrorMessage = "ID document must be less than 5MB";
                return Page();
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var fileExtension = Path.GetExtension(IdDocument.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                ErrorMessage = "ID document must be JPG, PNG, or PDF";
                return Page();
            }

            // Create uploads directory if it doesn't exist
            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "id-documents");
            Directory.CreateDirectory(uploadsDir);

            // Generate unique filename
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsDir, uniqueFileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await IdDocument.CopyToAsync(stream);
            }

            idDocumentPath = $"/uploads/id-documents/{uniqueFileName}";
        }

        // Generate verification token
        var verificationToken = Guid.NewGuid().ToString("N");
        
        // Create user (requires email verification)
        var user = new User
        {
            FullName = Input.FullName,
            Email = Input.Email,
            Phone = Input.Phone,
            Address = Input.Address,
            PasswordHash = PasswordHelper.HashPassword(Input.Password),
            IsRenter = Input.IsRenter,
            IsOwner = Input.IsOwner,
            IsAdmin = false, // Regular users are not admin
            IsEmailVerified = false, // Requires email verification
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24),
            IdDocumentUrl = idDocumentPath,
            IsVerifiedOwner = !Input.IsOwner, // Non-owners auto-verified, owners need verification
            VerificationStatus = Input.IsOwner ? "Pending" : "N/A",
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
            // Log error with details for debugging
            Log.Error(ex, "Failed to send verification email to {Email}. Error: {ErrorMessage}", user.Email, ex.Message);
            Console.WriteLine($"Email sending error: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Log inner exception if exists
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                Log.Error(ex.InnerException, "Inner exception details");
            }
            
            // Show more helpful error message in development, generic in production
            if (_environment.IsDevelopment())
            {
                TempData["ErrorMessage"] = $"Account created but failed to send verification email. Error: {ex.Message}. Check console/logs for details.";
            }
            else
            {
                TempData["ErrorMessage"] = "Account created but failed to send verification email. Please contact support.";
            }
        }

        return RedirectToPage("/Account/RegistrationSuccess");
    }
}

