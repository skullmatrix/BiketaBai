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
    public IFormFile? IdDocumentFront { get; set; }

    [BindProperty]
    public IFormFile? IdDocumentBack { get; set; }

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

        // Restore step from TempData if available
        if (TempData.ContainsKey("RenterCurrentStep"))
        {
            CurrentStep = int.TryParse(TempData["RenterCurrentStep"]?.ToString(), out var step) ? step : 1;
        }

        // Restore step 1 data if going back
        if (CurrentStep == 1 && TempData.ContainsKey("RenterFullName"))
        {
            Input.FullName = TempData["RenterFullName"]?.ToString() ?? "";
            Input.Email = TempData["RenterEmail"]?.ToString() ?? "";
            Input.Address = TempData["RenterAddress"]?.ToString() ?? "";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? step)
    {
        // Handle back button from step 2 FIRST (before parsing step as int)
        if (step == "back")
        {
            // User clicked back from step 2, restore step 1 data
            if (TempData.ContainsKey("RenterFullName"))
            {
                Input.FullName = TempData["RenterFullName"]?.ToString() ?? "";
                Input.Email = TempData["RenterEmail"]?.ToString() ?? "";
                Input.Address = TempData["RenterAddress"]?.ToString() ?? "";
            }
            CurrentStep = 1;
            TempData["RenterCurrentStep"] = "1";
            return Page();
        }

        CurrentStep = int.TryParse(step, out var stepNum) ? stepNum : 1;

        // Step 1: Basic Information
        if (CurrentStep == 1)
        {
            // Clear ModelState errors for fields we're manually validating
            ModelState.Clear();
            
            if (string.IsNullOrWhiteSpace(Input.FullName) || 
                string.IsNullOrWhiteSpace(Input.Email) || 
                string.IsNullOrWhiteSpace(Input.Address))
            {
                ErrorMessage = "Please fill in all required fields";
                CurrentStep = 1;
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
            TempData["RenterCurrentStep"] = "2";
            CurrentStep = 2;
            return Page();
        }

        // Step 2: ID Document (Front and Back)
        if (CurrentStep == 2)
        {
            // Validate that files are present
            if (IdDocumentFront == null || IdDocumentFront.Length == 0)
            {
                ErrorMessage = "ID front photo is required";
                CurrentStep = 2;
                TempData["RenterCurrentStep"] = "2";
                return Page();
            }

            if (IdDocumentBack == null || IdDocumentBack.Length == 0)
            {
                ErrorMessage = "ID back photo is required";
                CurrentStep = 2;
                TempData["RenterCurrentStep"] = "2";
                return Page();
            }

            // Validate file sizes (max 5MB each)
            if (IdDocumentFront.Length > 5 * 1024 * 1024 || IdDocumentBack.Length > 5 * 1024 * 1024)
            {
                ErrorMessage = "ID photos must be less than 5MB each";
                CurrentStep = 2;
                return Page();
            }

            // Validate file types (images only, no PDF for camera capture)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var frontExtension = Path.GetExtension(IdDocumentFront.FileName).ToLowerInvariant();
            var backExtension = Path.GetExtension(IdDocumentBack.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(frontExtension) || !allowedExtensions.Contains(backExtension))
            {
                ErrorMessage = "ID photos must be JPG or PNG images";
                CurrentStep = 2;
                return Page();
            }

            // Save ID documents
            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "id-documents");
            Directory.CreateDirectory(uploadsDir);
            
            // Save front
            var frontFileName = $"{Guid.NewGuid()}_front{frontExtension}";
            var frontFilePath = Path.Combine(uploadsDir, frontFileName);
            using (var stream = new FileStream(frontFilePath, FileMode.Create))
            {
                await IdDocumentFront.CopyToAsync(stream);
            }
            var idDocumentFrontPath = $"/uploads/id-documents/{frontFileName}";

            // Save back
            var backFileName = $"{Guid.NewGuid()}_back{backExtension}";
            var backFilePath = Path.Combine(uploadsDir, backFileName);
            using (var stream = new FileStream(backFilePath, FileMode.Create))
            {
                await IdDocumentBack.CopyToAsync(stream);
            }
            var idDocumentBackPath = $"/uploads/id-documents/{backFileName}";

            // Validate ID using IdValidationService with OCR (read from saved file)
            IdValidationService.IdValidationResult idValidation;
            try
            {
                // Read the saved file and create a FormFile for validation
                using var fileStream = System.IO.File.OpenRead(frontFilePath);
                var memoryStream = new MemoryStream();
                await fileStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                
                var formFile = new Microsoft.AspNetCore.Http.FormFile(
                    memoryStream, 
                    0, 
                    memoryStream.Length, 
                    IdDocumentFront.Name, 
                    IdDocumentFront.FileName)
                {
                    Headers = new Microsoft.AspNetCore.Http.HeaderDictionary(),
                    ContentType = IdDocumentFront.ContentType
                };
                
                idValidation = await _idValidationService.ValidateIdAsync(formFile);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during ID OCR validation. Proceeding with basic validation.");
                // If OCR fails, still proceed - basic file validation already passed
                idValidation = new IdValidationService.IdValidationResult
                {
                    IsValid = true,
                    ErrorMessage = null
                };
            }

            if (!idValidation.IsValid)
            {
                ErrorMessage = idValidation.ErrorMessage ?? "Invalid ID document";
                CurrentStep = 2;
                TempData["RenterCurrentStep"] = "2";
                return Page();
            }

            // Extract address from ID using OCR (use front)
            string? extractedAddress = idValidation.ExtractedAddress;
            string? extractedName = idValidation.ExtractedName;
            
            // If OCR successfully extracted name or address, mark as verified
            bool isVerified = !string.IsNullOrWhiteSpace(extractedName) || !string.IsNullOrWhiteSpace(extractedAddress);
            
            if (isVerified)
            {
                Log.Information("ID automatically verified via OCR. Name: {Name}, Address: {Address}", 
                    extractedName, extractedAddress);
            }
            else
            {
                Log.Warning("OCR did not extract name or address from ID. Manual verification may be required.");
            }
            
            TempData["RenterIdDocumentFront"] = idDocumentFrontPath;
            TempData["RenterIdDocumentBack"] = idDocumentBackPath;
            TempData["RenterIdExtractedAddress"] = extractedAddress ?? "";
            TempData["RenterIdExtractedName"] = extractedName ?? "";
            TempData["RenterIdVerified"] = isVerified ? "true" : "pending";
            TempData["RenterCurrentStep"] = "3";
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
            var idDocumentFrontPath = TempData["RenterIdDocumentFront"]?.ToString();
            var idDocumentBackPath = TempData["RenterIdDocumentBack"]?.ToString();
            var idExtractedAddress = TempData["RenterIdExtractedAddress"]?.ToString();
            var addressVerified = TempData["RenterAddressVerified"]?.ToString() == "true";
            var idVerified = TempData["RenterIdVerified"]?.ToString() == "true";
            
            // Combine front and back paths (store front in IdDocumentUrl, back can be stored separately if needed)
            var idDocumentPath = idDocumentFrontPath; // Store front as primary

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

