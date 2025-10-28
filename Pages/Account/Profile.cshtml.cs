using BiketaBai.Data;
using BiketaBai.Models;
using BiketaBai.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace BiketaBai.Pages.Account
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;
        private readonly UserProfileService _profileService;

        public ProfileModel(BiketaBaiDbContext context, UserProfileService profileService)
        {
            _context = context;
            _profileService = profileService;
        }

        public User CurrentUser { get; set; } = null!;
        public UserStatistics Statistics { get; set; } = new UserStatistics();
        public List<Booking> RecentBookings { get; set; } = new List<Booking>();

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Full name is required")]
            [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters")]
            [Display(Name = "Full Name")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Phone number is required")]
            [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
            [Phone(ErrorMessage = "Please enter a valid phone number")]
            [Display(Name = "Phone Number")]
            public string Phone { get; set; } = string.Empty;

            [Required(ErrorMessage = "Address is required")]
            [StringLength(255, ErrorMessage = "Address cannot exceed 255 characters")]
            [Display(Name = "Address")]
            public string Address { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Please log in to view your profile.";
                return RedirectToPage("/Account/Login");
            }

            try
            {
                CurrentUser = await _profileService.GetUserByIdAsync(userId.Value);
                if (CurrentUser == null)
                {
                    TempData["ErrorMessage"] = "User account not found. Please log in again.";
                    return RedirectToPage("/Account/Login");
                }

                // Load user statistics
                Statistics = await _profileService.GetUserStatisticsAsync(userId.Value);

                // Load recent bookings
                if (CurrentUser.IsOwner || CurrentUser.IsRenter)
                {
                    RecentBookings = await _profileService.GetRecentBookingsAsync(
                        userId.Value, 
                        CurrentUser.IsOwner, 
                        5
                    ) ?? new List<Booking>();
                }

                // Populate input model with current values
                Input.FullName = CurrentUser.FullName ?? string.Empty;
                Input.Phone = CurrentUser.Phone ?? string.Empty;
                Input.Address = CurrentUser.Address ?? string.Empty;

                return Page();
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while loading your profile. Please try again.";
                // Log the error (assuming logging is set up)
                Console.WriteLine($"Profile Load Error: {ex.Message}");
                
                // Set minimal defaults to prevent null reference errors
                CurrentUser = new User 
                { 
                    FullName = "Unknown", 
                    Email = User.Identity?.Name ?? "unknown@example.com",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                Statistics = new UserStatistics();
                RecentBookings = new List<Booking>();
                Input = new InputModel();
                
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Please log in to update your profile.";
                return RedirectToPage("/Account/Login");
            }

            if (!ModelState.IsValid)
            {
                ErrorMessage = "Please correct the errors in the form";
                return await OnGetAsync();
            }

            try
            {
                // Update profile using service
                var (success, message) = await _profileService.UpdateProfileAsync(
                    userId.Value,
                    Input.FullName,
                    Input.Phone,
                    Input.Address
                );

                if (success)
                {
                    SuccessMessage = message;
                    return RedirectToPage();
                }
                else
                {
                    ErrorMessage = message;
                    return await OnGetAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while updating your profile. Please try again.";
                Console.WriteLine($"Profile Update Error: {ex.Message}");
                return await OnGetAsync();
            }
        }

        public async Task<IActionResult> OnPostUploadPhotoAsync(IFormFile profilePhoto)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Please log in to upload a photo.";
                return RedirectToPage("/Account/Login");
            }

            if (profilePhoto == null || profilePhoto.Length == 0)
            {
                ErrorMessage = "Please select a photo to upload";
                return RedirectToPage();
            }

            try
            {
                // Save uploaded photo
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var (success, filePath, message) = await _profileService.SaveUploadedFileAsync(
                    profilePhoto,
                    "uploads/profiles",
                    allowedExtensions,
                    5 * 1024 * 1024 // 5MB max
                );

                if (success && filePath != null)
                {
                    // Update profile photo in database
                    var (updateSuccess, updateMessage) = await _profileService.UpdateProfilePhotoAsync(userId.Value, filePath);
                    
                    if (updateSuccess)
                    {
                        SuccessMessage = "Profile photo updated successfully!";
                    }
                    else
                    {
                        ErrorMessage = updateMessage;
                    }
                }
                else
                {
                    ErrorMessage = message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while uploading your photo. Please try again.";
                Console.WriteLine($"Photo Upload Error: {ex.Message}");
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUploadIdDocumentAsync(IFormFile idDocument)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Please log in to upload a document.";
                return RedirectToPage("/Account/Login");
            }

            try
            {
                CurrentUser = await _profileService.GetUserByIdAsync(userId.Value);
                if (CurrentUser == null || !CurrentUser.IsOwner)
                {
                    ErrorMessage = "Only owners can upload ID documents";
                    return RedirectToPage();
                }

                if (idDocument == null || idDocument.Length == 0)
                {
                    ErrorMessage = "Please select a document to upload";
                    return RedirectToPage();
                }

                // Save uploaded ID document
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                var (success, filePath, message) = await _profileService.SaveUploadedFileAsync(
                    idDocument,
                    "uploads/documents",
                    allowedExtensions,
                    5 * 1024 * 1024 // 5MB max
                );

                if (success && filePath != null)
                {
                    // Update ID document in database
                    var (updateSuccess, updateMessage) = await _profileService.UpdateIdDocumentAsync(userId.Value, filePath);
                    
                    if (updateSuccess)
                    {
                        SuccessMessage = updateMessage;
                    }
                    else
                    {
                        ErrorMessage = updateMessage;
                    }
                }
                else
                {
                    ErrorMessage = message;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while uploading your document. Please try again.";
                Console.WriteLine($"Document Upload Error: {ex.Message}");
            }

            return RedirectToPage();
        }

        private int? GetCurrentUserId()
        {
            // Try the standard NameIdentifier claim first (for existing sessions)
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("UserId")?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return null;
            }
            return userId;
        }

        public string GetVerificationStatusBadge()
        {
            if (CurrentUser == null) return string.Empty;

            return CurrentUser.VerificationStatus switch
            {
                "Approved" => "<span class='badge bg-success'><i class='bi bi-patch-check-fill'></i> Verified</span>",
                "Pending" => "<span class='badge bg-warning'><i class='bi bi-hourglass-split'></i> Pending</span>",
                "Rejected" => "<span class='badge bg-danger'><i class='bi bi-x-circle-fill'></i> Rejected</span>",
                _ => "<span class='badge bg-secondary'><i class='bi bi-question-circle'></i> Not Verified</span>"
            };
        }
    }
}

