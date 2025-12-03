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
    private readonly EmailService _emailService;
    private readonly AddressValidationService _addressValidationService;
    private readonly IdValidationService _idValidationService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public RegisterOwnerModel(
        BiketaBaiDbContext context, 
        WalletService walletService, 
        EmailService emailService,
        AddressValidationService addressValidationService,
        IdValidationService idValidationService,
        IConfiguration configuration, 
        IWebHostEnvironment environment)
    {
        _context = context;
        _walletService = walletService;
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

        [Display(Name = "Store Address Place ID")]
        public string? StoreAddressPlaceId { get; set; }

        [Display(Name = "Store Address Latitude")]
        public double? StoreAddressLatitude { get; set; }

        [Display(Name = "Store Address Longitude")]
        public double? StoreAddressLongitude { get; set; }

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

    public async Task<IActionResult> OnGetAsync()
    {
        // Check if we have a current step in TempData (check this first before consuming TempData)
        var hasCurrentStep = TempData.ContainsKey("OwnerCurrentStep");
        
        // Restore step from TempData if available
        if (hasCurrentStep)
        {
            CurrentStep = int.TryParse(TempData["OwnerCurrentStep"]?.ToString(), out var step) ? step : 1;
            TempData.Keep("OwnerCurrentStep");
        }

        // Check if coming from type selection
        var registrationType = TempData["RegistrationType"]?.ToString();
        
        // If we have a current step set (step > 1 or we had it in TempData), we're in the middle of registration
        // This handles the case where ID validation fails and we redirect back
        if (CurrentStep > 1 || hasCurrentStep)
        {
            // We're in the middle of registration, ensure RegistrationType is set
            if (string.IsNullOrEmpty(registrationType))
            {
                // If RegistrationType is missing but we have a step, set it to Owner
                registrationType = "Owner";
                TempData["RegistrationType"] = "Owner";
            }
            else
            {
                TempData.Keep("RegistrationType");
            }
        }
        else if (!string.IsNullOrEmpty(registrationType))
        {
            // Keep RegistrationType in TempData so it persists across redirects
            TempData.Keep("RegistrationType");
        }
        
        // Only redirect to type selection if we're on step 1 and registration type is wrong or missing
        if (CurrentStep == 1 && string.IsNullOrEmpty(registrationType))
        {
            // Redirect to type selection if not coming from there
            return RedirectToPage("/Account/RegisterType");
        }

        // If we have a step > 1, we're definitely in registration, so don't redirect
        if (CurrentStep > 1 && registrationType != "Owner")
        {
            // Ensure RegistrationType is set correctly
            TempData["RegistrationType"] = "Owner";
        }

        // Restore step 1 data if on step 1
        if (CurrentStep == 1 && TempData.ContainsKey("OwnerFullName"))
        {
            Input.FullName = TempData["OwnerFullName"]?.ToString() ?? "";
            Input.Email = TempData["OwnerEmail"]?.ToString() ?? "";
            Input.StoreName = TempData["OwnerStoreName"]?.ToString() ?? "";
            Input.StoreAddress = TempData["OwnerStoreAddress"]?.ToString() ?? "";
        }

        // Restore step 2 data if on step 2 (password)
        if (CurrentStep == 2 && TempData.ContainsKey("OwnerPassword"))
        {
            Input.Password = TempData["OwnerPassword"]?.ToString() ?? "";
            Input.ConfirmPassword = TempData["OwnerPassword"]?.ToString() ?? "";
        }

        // Check for error messages from TempData (from POST redirect)
        // Don't keep error messages - they should only display once
        if (TempData.ContainsKey("OwnerErrorMessage"))
        {
            ErrorMessage = TempData["OwnerErrorMessage"]?.ToString();
            // Explicitly remove error message to prevent it from persisting
            TempData.Remove("OwnerErrorMessage");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? step)
    {
        // Handle back button FIRST (before parsing step as int)
        if (step == "back")
        {
            var currentStepFromTemp = int.TryParse(TempData["OwnerCurrentStep"]?.ToString(), out var tempStep) ? tempStep : 1;
            
            if (currentStepFromTemp == 3)
            {
                // Going back from step 3 (ID upload) to step 2 (password)
                CurrentStep = 2;
                TempData["OwnerCurrentStep"] = "2";
                if (TempData.ContainsKey("OwnerPassword"))
                {
                    Input.Password = TempData["OwnerPassword"]?.ToString() ?? "";
                    Input.ConfirmPassword = TempData["OwnerPassword"]?.ToString() ?? "";
                }
            }
            else if (currentStepFromTemp == 2)
            {
                // Going back from step 2 (password) to step 1 (basic info)
                CurrentStep = 1;
                TempData["OwnerCurrentStep"] = "1";
            if (TempData.ContainsKey("OwnerFullName"))
            {
                Input.FullName = TempData["OwnerFullName"]?.ToString() ?? "";
                Input.Email = TempData["OwnerEmail"]?.ToString() ?? "";
                Input.StoreName = TempData["OwnerStoreName"]?.ToString() ?? "";
                Input.StoreAddress = TempData["OwnerStoreAddress"]?.ToString() ?? "";
            }
            }
            return Page();
        }

        CurrentStep = int.TryParse(step, out var stepNum) ? stepNum : 1;

        // Store current step in TempData for persistence
        TempData["OwnerCurrentStep"] = CurrentStep.ToString();

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
            // If coordinates are provided from autocomplete, use them directly
            AddressValidationService.AddressValidationResult addressValidation;
            if (Input.StoreAddressLatitude.HasValue && Input.StoreAddressLongitude.HasValue)
            {
                // Address was selected from autocomplete with coordinates - validate using coordinates
                addressValidation = await _addressValidationService.ValidateAddressAsync(
                    Input.StoreAddress, 
                    Input.StoreAddressLatitude.Value, 
                    Input.StoreAddressLongitude.Value
                );
            }
            else
            {
                // No coordinates provided - validate by searching
                addressValidation = await _addressValidationService.ValidateAddressAsync(Input.StoreAddress);
            }

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
            TempData["OwnerCurrentStep"] = "2";
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
                TempData["OwnerCurrentStep"] = "2";
                return Page();
            }

            if (Input.Password != Input.ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match";
                CurrentStep = 2;
                TempData["OwnerCurrentStep"] = "2";
                return Page();
            }

            TempData["OwnerPassword"] = Input.Password;
            TempData["OwnerCurrentStep"] = "3";
            CurrentStep = 3;
            return Page();
        }

        // Step 3: ID Document (Front and Back)
        if (CurrentStep == 3)
        {
            // Validate that files are present
            if (IdDocumentFront == null || IdDocumentFront.Length == 0)
            {
                TempData["OwnerErrorMessage"] = "ID front photo is required";
                TempData["OwnerCurrentStep"] = "3";
                TempData.Keep("RegistrationType");
                return RedirectToPage();
            }

            if (IdDocumentBack == null || IdDocumentBack.Length == 0)
            {
                TempData["OwnerErrorMessage"] = "ID back photo is required";
                TempData["OwnerCurrentStep"] = "3";
                TempData.Keep("RegistrationType");
                return RedirectToPage();
            }

            // Compress images automatically if they're too large (no size limit, auto-compress to 5MB)

            // Validate file types (images only, no PDF for camera capture)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var frontExtension = Path.GetExtension(IdDocumentFront.FileName).ToLowerInvariant();
            var backExtension = Path.GetExtension(IdDocumentBack.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(frontExtension) || !allowedExtensions.Contains(backExtension))
            {
                TempData["OwnerErrorMessage"] = "ID photos must be JPG or PNG images";
                TempData["OwnerCurrentStep"] = "3";
                TempData.Keep("RegistrationType");
                return RedirectToPage();
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

            // Handle business license (optional, camera capture)
            string? businessLicensePath = null;
            if (BusinessLicense != null)
            {
                if (BusinessLicense.Length > 5 * 1024 * 1024)
                {
                    TempData["OwnerErrorMessage"] = "Business license must be less than 5MB";
                    TempData["OwnerCurrentStep"] = "3";
                    TempData.Keep("RegistrationType");
                    return RedirectToPage();
                }

                var licenseExtension = Path.GetExtension(BusinessLicense.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(licenseExtension))
                {
                    TempData["OwnerErrorMessage"] = "Business license must be JPG or PNG image";
                    TempData["OwnerCurrentStep"] = "3";
                    TempData.Keep("RegistrationType");
                    return RedirectToPage();
                }

                var licenseFileName = $"{Guid.NewGuid()}_license{licenseExtension}";
                var licenseFilePath = Path.Combine(uploadsDir, licenseFileName);

                using (var stream = new FileStream(licenseFilePath, FileMode.Create))
                {
                    await BusinessLicense.CopyToAsync(stream);
                }

                businessLicensePath = $"/uploads/id-documents/{licenseFileName}";
            }

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
                Log.Error(ex, "Error during ID OCR validation. Cannot verify ID authenticity.");
                // If OCR fails, we cannot verify if it's a valid ID - reject it
                idValidation = new IdValidationService.IdValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Please upload a valid ID"
                };
            }

            if (!idValidation.IsValid)
            {
                // Store error in TempData and redirect to prevent POST resubmission on refresh
                TempData["OwnerErrorMessage"] = idValidation.ErrorMessage ?? "Please upload a valid ID";
                TempData["OwnerCurrentStep"] = "3";
                // Keep RegistrationType so OnGetAsync doesn't redirect to RegisterType
                TempData.Keep("RegistrationType");
                return RedirectToPage();
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

            // Get password from TempData before storing ID data (to preserve it)
            var password = TempData["OwnerPassword"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(password))
            {
                ErrorMessage = "Password is required. Please go back and set your password.";
                CurrentStep = 3;
                TempData["OwnerCurrentStep"] = "3";
                return Page();
            }

            // Keep all TempData values to ensure they persist
            TempData.Keep("OwnerFullName");
            TempData.Keep("OwnerEmail");
            TempData.Keep("OwnerStoreName");
            TempData.Keep("OwnerStoreAddress");
            TempData.Keep("OwnerAddressVerified");
            TempData.Keep("OwnerPassword");

            TempData["OwnerIdDocumentFront"] = idDocumentFrontPath;
            TempData["OwnerIdDocumentBack"] = idDocumentBackPath;
            TempData["OwnerBusinessLicense"] = businessLicensePath;
            TempData["OwnerIdExtractedAddress"] = extractedAddress ?? "";
            TempData["OwnerIdExtractedName"] = extractedName ?? "";
            TempData["OwnerIdVerified"] = isVerified ? "true" : "pending";

            // Proceed to create account - pass all required data directly
            return await CreateOwnerAccountAsync(
                password: password,
                fullName: TempData["OwnerFullName"]?.ToString() ?? "",
                email: TempData["OwnerEmail"]?.ToString() ?? "",
                storeName: TempData["OwnerStoreName"]?.ToString() ?? "",
                storeAddress: TempData["OwnerStoreAddress"]?.ToString() ?? "",
                idDocumentFrontPath: idDocumentFrontPath,
                idDocumentBackPath: idDocumentBackPath,
                idExtractedAddress: extractedAddress ?? "",
                addressVerified: TempData["OwnerAddressVerified"]?.ToString() == "true",
                idVerified: isVerified
            );
        }

        return Page();
    }

    private async Task<IActionResult> CreateOwnerAccountAsync(
        string password,
        string fullName,
        string email,
        string storeName,
        string storeAddress,
        string idDocumentFrontPath,
        string? idDocumentBackPath,
        string? idExtractedAddress,
        bool addressVerified,
        bool idVerified)
    {
        // Validate required parameters
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) || 
            string.IsNullOrEmpty(storeName) || string.IsNullOrEmpty(storeAddress) ||
            string.IsNullOrEmpty(idDocumentFrontPath))
            {
            Log.Error("Missing required data for owner account creation. FullName: {HasFullName}, Email: {HasEmail}, StoreName: {HasStoreName}, StoreAddress: {HasStoreAddress}, IdDocument: {HasIdDocument}",
                !string.IsNullOrEmpty(fullName), !string.IsNullOrEmpty(email), 
                !string.IsNullOrEmpty(storeName), !string.IsNullOrEmpty(storeAddress),
                !string.IsNullOrEmpty(idDocumentFrontPath));
                ErrorMessage = "Session expired. Please start over.";
                return RedirectToPage("/Account/RegisterType");
            }
        
        // Store front as primary ID document
        var idDocumentPath = idDocumentFrontPath;

            // Validate password
        if (!PasswordHelper.IsPasswordMedium(password))
            {
                ErrorMessage = "Password must be at least 6 characters";
                CurrentStep = 3;
            TempData["OwnerCurrentStep"] = "3";
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
            PasswordHash = PasswordHelper.HashPassword(password),
                IsRenter = false,
                IsOwner = true,
                IsAdmin = false,
                IsEmailVerified = false,
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpires = DateTime.UtcNow.AddHours(24),
                IdDocumentUrl = idDocumentFrontPath,
                IdDocumentBackUrl = idDocumentBackPath,
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

            // Create wallet
            await _walletService.GetOrCreateWalletAsync(user.UserId);

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
}

