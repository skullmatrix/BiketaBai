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

public class RegisterRenterModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly WalletService _walletService;
    private readonly PointsService _pointsService;
    private readonly EmailService _emailService;
    private readonly AddressValidationService _addressValidationService;
    private readonly IdValidationService _idValidationService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public RegisterRenterModel(
        BiketaBaiDbContext context, 
        WalletService walletService, 
        PointsService pointsService, 
        EmailService emailService,
        AddressValidationService addressValidationService,
        IdValidationService idValidationService,
        IConfiguration configuration, 
        IWebHostEnvironment environment)
    {
        _context = context;
        _walletService = walletService;
        _pointsService = pointsService;
        _emailService = emailService;
        _addressValidationService = addressValidationService;
        _idValidationService = idValidationService;
        _configuration = configuration;
        _environment = environment;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? IdDocument { get; set; }

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public int CurrentStep { get; set; } = 1;

    public class InputModel
    {
        // Step 1: Basic Information
        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Address")]
        public string Address { get; set; } = string.Empty;

        // Step 2: ID Document (handled via IdDocument property)

        // Step 3: Account Setup
        [Required]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        // Check if coming from type selection
        var registrationType = TempData["RegistrationType"]?.ToString();
        if (registrationType != "Renter")
        {
            // Redirect to type selection if not coming from there
            return RedirectToPage("/Account/RegisterType");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? step)
    {
        CurrentStep = int.TryParse(step, out var stepNum) ? stepNum : 1;

        if (!ModelState.IsValid && CurrentStep > 1)
        {
            return Page();
        }

        // Step 1: Basic Information
        if (CurrentStep == 1)
        {
            if (string.IsNullOrWhiteSpace(Input.FullName) || 
                string.IsNullOrWhiteSpace(Input.Email) || 
                string.IsNullOrWhiteSpace(Input.Address))
            {
                ErrorMessage = "Please fill in all required fields";
                return Page();
            }

            // Check if email already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == Input.Email);
            if (existingUser != null)
            {
                ErrorMessage = "Email address is already registered";
                return Page();
            }

            // Validate address using AddressValidationService
            var addressValidation = await _addressValidationService.ValidateAddressAsync(Input.Address);
            if (!addressValidation.IsValid)
            {
                ErrorMessage = addressValidation.ErrorMessage ?? "Invalid address. Please check and try again.";
                return Page();
            }

            // Use standardized address if available
            var validatedAddress = addressValidation.StandardizedAddress ?? Input.Address;

            TempData["RenterFullName"] = Input.FullName;
            TempData["RenterEmail"] = Input.Email;
            TempData["RenterAddress"] = validatedAddress;
            TempData["RenterAddressVerified"] = "true";
            CurrentStep = 2;
            return Page();
        }

        // Step 2: ID Document
        if (CurrentStep == 2)
        {
            if (IdDocument == null)
            {
                ErrorMessage = "ID document is required";
                CurrentStep = 2;
                return Page();
            }

            // Validate file size (max 5MB)
            if (IdDocument.Length > 5 * 1024 * 1024)
            {
                ErrorMessage = "ID document must be less than 5MB";
                CurrentStep = 2;
                return Page();
            }

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var fileExtension = Path.GetExtension(IdDocument.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                ErrorMessage = "ID document must be JPG, PNG, or PDF";
                CurrentStep = 2;
                return Page();
            }

            // Save ID document
            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "id-documents");
            Directory.CreateDirectory(uploadsDir);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsDir, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await IdDocument.CopyToAsync(stream);
            }

            var idDocumentPath = $"/uploads/id-documents/{uniqueFileName}";

            // Validate ID using IdValidationService
            var idValidation = await _idValidationService.ValidateIdAsync(IdDocument);
            if (!idValidation.IsValid)
            {
                ErrorMessage = idValidation.ErrorMessage ?? "Invalid ID document";
                CurrentStep = 2;
                return Page();
            }

            // TODO: Extract address from ID using OCR
            // For now, we'll cross-check later when OCR is implemented
            var extractedAddress = await _idValidationService.ExtractAddressFromIdAsync(IdDocument);
            
            TempData["RenterIdDocument"] = idDocumentPath;
            TempData["RenterIdExtractedAddress"] = extractedAddress ?? "";
            TempData["RenterIdVerified"] = "true";
            CurrentStep = 3;
            return Page();
        }

        // Step 3: Account Setup
        if (CurrentStep == 3)
        {
            // Retrieve data from TempData
            var fullName = TempData["RenterFullName"]?.ToString();
            var email = TempData["RenterEmail"]?.ToString();
            var address = TempData["RenterAddress"]?.ToString();
            var idDocumentPath = TempData["RenterIdDocument"]?.ToString();
            var idExtractedAddress = TempData["RenterIdExtractedAddress"]?.ToString();
            var addressVerified = TempData["RenterAddressVerified"]?.ToString() == "true";
            var idVerified = TempData["RenterIdVerified"]?.ToString() == "true";

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(address))
            {
                ErrorMessage = "Session expired. Please start over.";
                return RedirectToPage("/Account/RegisterType");
            }

            // Validate password
            if (!PasswordHelper.IsPasswordMedium(Input.Password))
            {
                ErrorMessage = "Password must be at least 6 characters";
                CurrentStep = 3;
                return Page();
            }

            // Create user
            var verificationToken = Guid.NewGuid().ToString("N");
            var user = new User
            {
                FullName = fullName,
                Email = email,
                Address = address,
                PasswordHash = PasswordHelper.HashPassword(Input.Password),
                IsRenter = true,
                IsOwner = false,
                IsAdmin = false,
                IsEmailVerified = false,
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24),
                IdDocumentUrl = idDocumentPath,
                IdVerified = idVerified,
                IdVerifiedAt = idVerified ? DateTime.UtcNow : null,
                AddressVerified = addressVerified,
                AddressVerifiedAt = addressVerified ? DateTime.UtcNow : null,
                IdExtractedAddress = idExtractedAddress,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Cross-check address if ID address was extracted
            if (!string.IsNullOrEmpty(idExtractedAddress) && !string.IsNullOrEmpty(address))
            {
                var addressMatch = await _idValidationService.CrossCheckAddressAsync(address, idExtractedAddress);
                if (!addressMatch)
                {
                    Log.Warning("Address mismatch for user {Email}: User address='{UserAddress}', ID address='{IdAddress}'", 
                        email, address, idExtractedAddress);
                    // Don't block registration, but log the mismatch
                    // Admin can review later
                }
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Create wallet and points
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
                Log.Error(ex, "Failed to send verification email to {Email}", user.Email);
                TempData["ErrorMessage"] = "Account created but failed to send verification email. Please contact support.";
            }

            return RedirectToPage("/Account/RegistrationSuccess");
        }

        return Page();
    }
}

