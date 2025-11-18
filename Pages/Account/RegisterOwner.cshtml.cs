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

public class RegisterOwnerModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly WalletService _walletService;
    private readonly PointsService _pointsService;
    private readonly EmailService _emailService;
    private readonly AddressValidationService _addressValidationService;
    private readonly IdValidationService _idValidationService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public RegisterOwnerModel(
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

    [BindProperty]
    public IFormFile? BusinessLicense { get; set; }

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
        [Display(Name = "Store Name")]
        public string StoreName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Store Address")]
        public string StoreAddress { get; set; } = string.Empty;

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
        if (registrationType != "Owner")
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
                string.IsNullOrWhiteSpace(Input.StoreName) ||
                string.IsNullOrWhiteSpace(Input.StoreAddress))
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

            // Validate store address using AddressValidationService
            var addressValidation = await _addressValidationService.ValidateAddressAsync(Input.StoreAddress);
            if (!addressValidation.IsValid)
            {
                ErrorMessage = addressValidation.ErrorMessage ?? "Invalid store address. Please check and try again.";
                return Page();
            }

            // Use standardized address if available
            var validatedAddress = addressValidation.StandardizedAddress ?? Input.StoreAddress;

            TempData["OwnerFullName"] = Input.FullName;
            TempData["OwnerEmail"] = Input.Email;
            TempData["OwnerStoreName"] = Input.StoreName;
            TempData["OwnerStoreAddress"] = validatedAddress;
            TempData["OwnerAddressVerified"] = "true";
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

            // Handle business license (optional)
            string? businessLicensePath = null;
            if (BusinessLicense != null)
            {
                if (BusinessLicense.Length > 5 * 1024 * 1024)
                {
                    ErrorMessage = "Business license must be less than 5MB";
                    CurrentStep = 2;
                    return Page();
                }

                var licenseExtension = Path.GetExtension(BusinessLicense.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(licenseExtension))
                {
                    ErrorMessage = "Business license must be JPG, PNG, or PDF";
                    CurrentStep = 2;
                    return Page();
                }

                var licenseFileName = $"{Guid.NewGuid()}{licenseExtension}";
                var licenseFilePath = Path.Combine(uploadsDir, licenseFileName);

                using (var stream = new FileStream(licenseFilePath, FileMode.Create))
                {
                    await BusinessLicense.CopyToAsync(stream);
                }

                businessLicensePath = $"/uploads/id-documents/{licenseFileName}";
            }

            // Validate ID using IdValidationService
            var idValidation = await _idValidationService.ValidateIdAsync(IdDocument);
            if (!idValidation.IsValid)
            {
                ErrorMessage = idValidation.ErrorMessage ?? "Invalid ID document";
                CurrentStep = 2;
                return Page();
            }

            // TODO: Extract address from ID using OCR
            var extractedAddress = await _idValidationService.ExtractAddressFromIdAsync(IdDocument);

            TempData["OwnerIdDocument"] = idDocumentPath;
            TempData["OwnerBusinessLicense"] = businessLicensePath;
            TempData["OwnerIdExtractedAddress"] = extractedAddress ?? "";
            TempData["OwnerIdVerified"] = "true";
            CurrentStep = 3;
            return Page();
        }

        // Step 3: Account Setup
        if (CurrentStep == 3)
        {
            // Retrieve data from TempData
            var fullName = TempData["OwnerFullName"]?.ToString();
            var email = TempData["OwnerEmail"]?.ToString();
            var storeName = TempData["OwnerStoreName"]?.ToString();
            var storeAddress = TempData["OwnerStoreAddress"]?.ToString();
            var idDocumentPath = TempData["OwnerIdDocument"]?.ToString();
            var idExtractedAddress = TempData["OwnerIdExtractedAddress"]?.ToString();
            var addressVerified = TempData["OwnerAddressVerified"]?.ToString() == "true";
            var idVerified = TempData["OwnerIdVerified"]?.ToString() == "true";

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || 
                string.IsNullOrEmpty(storeName) || string.IsNullOrEmpty(storeAddress))
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
                StoreName = storeName,
                StoreAddress = storeAddress,
                PasswordHash = PasswordHelper.HashPassword(Input.Password),
                IsRenter = false,
                IsOwner = true,
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
                IsVerifiedOwner = false, // Requires admin verification
                VerificationStatus = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Cross-check store address with ID address if available
            if (!string.IsNullOrEmpty(idExtractedAddress) && !string.IsNullOrEmpty(storeAddress))
            {
                var addressMatch = await _idValidationService.CrossCheckAddressAsync(storeAddress, idExtractedAddress);
                if (!addressMatch)
                {
                    Log.Warning("Address mismatch for owner {Email}: Store address='{StoreAddress}', ID address='{IdAddress}'", 
                        email, storeAddress, idExtractedAddress);
                    // Don't block registration, but log the mismatch
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
                TempData["SuccessMessage"] = "Registration successful! Please check your email to verify your account. Your owner account will be reviewed by our team.";
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

