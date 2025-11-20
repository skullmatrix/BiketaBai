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

        // Restore step 1 data if on step 1
        if (CurrentStep == 1 && TempData.ContainsKey("RenterFullName"))
        {
            Input.FullName = TempData["RenterFullName"]?.ToString() ?? "";
            Input.Email = TempData["RenterEmail"]?.ToString() ?? "";
            Input.Address = TempData["RenterAddress"]?.ToString() ?? "";
        }

        // Restore step 2 data if on step 2 (password)
        if (CurrentStep == 2 && TempData.ContainsKey("RenterPassword"))
        {
            Input.Password = TempData["RenterPassword"]?.ToString() ?? "";
            Input.ConfirmPassword = TempData["RenterPassword"]?.ToString() ?? "";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? step)
    {
        // Handle back button FIRST (before parsing step as int)
        if (step == "back")
        {
            // Determine which step we're going back from
            var currentStepFromTemp = int.TryParse(TempData["RenterCurrentStep"]?.ToString(), out var tempStep) ? tempStep : 1;
            
            if (currentStepFromTemp == 3)
            {
                // Going back from step 3 (ID upload) to step 2 (password)
                CurrentStep = 2;
                TempData["RenterCurrentStep"] = "2";
                // Restore password from TempData if available
                if (TempData.ContainsKey("RenterPassword"))
                {
                    Input.Password = TempData["RenterPassword"]?.ToString() ?? "";
                    Input.ConfirmPassword = TempData["RenterPassword"]?.ToString() ?? "";
                }
            }
            else if (currentStepFromTemp == 2)
            {
                // Going back from step 2 (password) to step 1 (basic info)
                if (TempData.ContainsKey("RenterFullName"))
                {
                    Input.FullName = TempData["RenterFullName"]?.ToString() ?? "";
                    Input.Email = TempData["RenterEmail"]?.ToString() ?? "";
                    Input.Address = TempData["RenterAddress"]?.ToString() ?? "";
                }
                CurrentStep = 1;
                TempData["RenterCurrentStep"] = "1";
            }
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

        // Step 2: Account Setup (Password)
        if (CurrentStep == 2)
        {
            // Clear ModelState errors for password fields
            ModelState.Clear();
            
            if (string.IsNullOrWhiteSpace(Input.Password) || Input.Password.Length < 6)
            {
                ErrorMessage = "Password must be at least 6 characters";
                CurrentStep = 2;
                TempData["RenterCurrentStep"] = "2";
                return Page();
            }

            if (Input.Password != Input.ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match";
                CurrentStep = 2;
                TempData["RenterCurrentStep"] = "2";
                return Page();
            }

            TempData["RenterPassword"] = Input.Password;
            TempData["RenterCurrentStep"] = "3";
            CurrentStep = 3;
            return Page();
        }

        // Step 3: ID Document (Front and Back) - Now with Google Vision verification
        if (CurrentStep == 3)
        {
            // Validate that files are present
            if (IdDocumentFront == null || IdDocumentFront.Length == 0)
            {
                ErrorMessage = "ID front photo is required";
                CurrentStep = 3;
                TempData["RenterCurrentStep"] = "3";
                return Page();
            }

            if (IdDocumentBack == null || IdDocumentBack.Length == 0)
            {
                ErrorMessage = "ID back photo is required";
                CurrentStep = 3;
                TempData["RenterCurrentStep"] = "3";
                return Page();
            }

            // Compress images automatically if they're too large (no size limit, auto-compress to 5MB)

            // Validate file types (images only, no PDF for camera capture)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var frontExtension = Path.GetExtension(IdDocumentFront.FileName).ToLowerInvariant();
            var backExtension = Path.GetExtension(IdDocumentBack.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(frontExtension) || !allowedExtensions.Contains(backExtension))
            {
                ErrorMessage = "ID photos must be JPG or PNG images";
                CurrentStep = 3;
                TempData["RenterCurrentStep"] = "3";
                return Page();
            }

            // Compress and save ID documents
            var uploadsDir = Path.Combine(_environment.WebRootPath, "uploads", "id-documents");
            Directory.CreateDirectory(uploadsDir);
            
            // Compress and save front
            var frontFileName = $"{Guid.NewGuid()}_front{frontExtension}";
            var frontFilePath = Path.Combine(uploadsDir, frontFileName);
            byte[] frontCompressedBytes;
            try
            {
                frontCompressedBytes = await ImageCompressionHelper.CompressFormFileAsync(IdDocumentFront);
                await System.IO.File.WriteAllBytesAsync(frontFilePath, frontCompressedBytes);
                Log.Information("Compressed ID front: Original {OriginalSize} bytes -> Compressed {CompressedSize} bytes", 
                    IdDocumentFront.Length, frontCompressedBytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error compressing front ID image. Saving original.");
                // Fallback: save original if compression fails
                using (var stream = new FileStream(frontFilePath, FileMode.Create))
                {
                    await IdDocumentFront.CopyToAsync(stream);
                }
                frontCompressedBytes = await System.IO.File.ReadAllBytesAsync(frontFilePath);
            }
            var idDocumentFrontPath = $"/uploads/id-documents/{frontFileName}";

            // Compress and save back
            var backFileName = $"{Guid.NewGuid()}_back{backExtension}";
            var backFilePath = Path.Combine(uploadsDir, backFileName);
            byte[] backCompressedBytes;
            try
            {
                backCompressedBytes = await ImageCompressionHelper.CompressFormFileAsync(IdDocumentBack);
                await System.IO.File.WriteAllBytesAsync(backFilePath, backCompressedBytes);
                Log.Information("Compressed ID back: Original {OriginalSize} bytes -> Compressed {CompressedSize} bytes", 
                    IdDocumentBack.Length, backCompressedBytes.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error compressing back ID image. Saving original.");
                // Fallback: save original if compression fails
                using (var stream = new FileStream(backFilePath, FileMode.Create))
                {
                    await IdDocumentBack.CopyToAsync(stream);
                }
                backCompressedBytes = await System.IO.File.ReadAllBytesAsync(backFilePath);
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
                ErrorMessage = idValidation.ErrorMessage ?? "Invalid ID document. Please ensure the ID is clear and readable.";
                CurrentStep = 3;
                TempData["RenterCurrentStep"] = "3";
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
            
            // Get password from TempData
            var password = TempData["RenterPassword"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(password))
            {
                ErrorMessage = "Password is required. Please go back and set your password.";
                CurrentStep = 3;
                TempData["RenterCurrentStep"] = "3";
                return Page();
            }

            // Create user account
            var fullName = TempData["RenterFullName"]?.ToString() ?? "";
            var email = TempData["RenterEmail"]?.ToString() ?? "";
            var address = TempData["RenterAddress"]?.ToString() ?? "";
            
            TempData["RenterIdDocumentFront"] = idDocumentFrontPath;
            TempData["RenterIdDocumentBack"] = idDocumentBackPath;
            TempData["RenterIdExtractedAddress"] = extractedAddress ?? "";
            TempData["RenterIdExtractedName"] = extractedName ?? "";
            TempData["RenterIdVerified"] = isVerified ? "true" : "pending";
            
            // Proceed to create account
            return await CreateRenterAccountAsync(fullName, email, address, password, idDocumentFrontPath);
        }

        return Page();
    }

    private async Task<IActionResult> CreateRenterAccountAsync(string fullName, string email, string address, string password, string idDocumentPath)
    {
        // Retrieve additional data from TempData
        var idDocumentFrontPath = TempData["RenterIdDocumentFront"]?.ToString();
        var idDocumentBackPath = TempData["RenterIdDocumentBack"]?.ToString();
        var idExtractedAddress = TempData["RenterIdExtractedAddress"]?.ToString();
        var addressVerified = TempData["RenterAddressVerified"]?.ToString() == "true";
        var idVerified = TempData["RenterIdVerified"]?.ToString() == "true";

        if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(address))
        {
            ErrorMessage = "Session expired. Please start over.";
            return RedirectToPage("/Account/RegisterType");
        }

        // Validate password
        if (!PasswordHelper.IsPasswordMedium(password))
        {
            ErrorMessage = "Password must be at least 6 characters";
            CurrentStep = 3;
            TempData["RenterCurrentStep"] = "3";
            return Page();
        }

        // Create user
        var verificationToken = Guid.NewGuid().ToString("N");
        var user = new User
        {
            FullName = fullName,
            Email = email,
            Address = address,
            PasswordHash = PasswordHelper.HashPassword(password),
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
}

