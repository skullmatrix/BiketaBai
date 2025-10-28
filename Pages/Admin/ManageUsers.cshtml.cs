using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ManageUsersModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public ManageUsersModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public List<User> Users { get; set; } = new List<User>();
        public List<User> FilteredUsers { get; set; } = new List<User>();
        
        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? RoleFilter { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        // Statistics
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int SuspendedUsers { get; set; }
        public int VerifiedEmails { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            // Calculate statistics
            TotalUsers = Users.Count;
            ActiveUsers = Users.Count(u => !u.IsSuspended);
            SuspendedUsers = Users.Count(u => u.IsSuspended);
            VerifiedEmails = Users.Count(u => u.IsEmailVerified);

            // Apply filters
            FilteredUsers = Users;

            // Search filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilteredUsers = FilteredUsers
                    .Where(u => 
                        u.FullName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        u.Email.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                        u.Phone.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Role filter
            if (!string.IsNullOrWhiteSpace(RoleFilter))
            {
                FilteredUsers = RoleFilter switch
                {
                    "Renter" => FilteredUsers.Where(u => u.IsRenter).ToList(),
                    "Owner" => FilteredUsers.Where(u => u.IsOwner).ToList(),
                    "Admin" => FilteredUsers.Where(u => u.IsAdmin).ToList(),
                    _ => FilteredUsers
                };
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                FilteredUsers = StatusFilter switch
                {
                    "Active" => FilteredUsers.Where(u => !u.IsSuspended).ToList(),
                    "Suspended" => FilteredUsers.Where(u => u.IsSuspended).ToList(),
                    "EmailVerified" => FilteredUsers.Where(u => u.IsEmailVerified).ToList(),
                    "EmailNotVerified" => FilteredUsers.Where(u => !u.IsEmailVerified).ToList(),
                    _ => FilteredUsers
                };
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSuspendAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                ErrorMessage = "User not found";
                return RedirectToPage();
            }

            if (user.IsAdmin)
            {
                ErrorMessage = "Cannot suspend an administrator";
                return RedirectToPage();
            }

            user.IsSuspended = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            SuccessMessage = $"User '{user.FullName}' has been suspended successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUnsuspendAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                ErrorMessage = "User not found";
                return RedirectToPage();
            }

            user.IsSuspended = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            SuccessMessage = $"User '{user.FullName}' has been reactivated successfully";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                ErrorMessage = "User not found";
                return RedirectToPage();
            }

            if (user.IsAdmin)
            {
                ErrorMessage = "Cannot delete an administrator";
                return RedirectToPage();
            }

            // Check if user has active bookings
            var hasActiveBookings = await _context.Bookings
                .AnyAsync(b => b.RenterId == userId && 
                    (b.BookingStatus.StatusName == "Active" || b.BookingStatus.StatusName == "Confirmed"));

            if (hasActiveBookings)
            {
                ErrorMessage = "Cannot delete user with active bookings. Please cancel bookings first.";
                return RedirectToPage();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            SuccessMessage = $"User '{user.FullName}' has been permanently deleted";
            return RedirectToPage();
        }

        public string GetRoleBadges(User user)
        {
            var badges = new List<string>();
            
            if (user.IsAdmin) badges.Add("<span class='badge bg-danger me-1'>Admin</span>");
            if (user.IsOwner) badges.Add("<span class='badge bg-warning me-1'>Owner</span>");
            if (user.IsRenter) badges.Add("<span class='badge bg-info me-1'>Renter</span>");
            
            return string.Join("", badges);
        }

        public string GetStatusBadge(User user)
        {
            if (user.IsSuspended)
            {
                return "<span class='badge bg-danger'><i class='bi bi-ban'></i> Suspended</span>";
            }
            
            if (!user.IsEmailVerified)
            {
                return "<span class='badge bg-warning'><i class='bi bi-envelope-x'></i> Unverified</span>";
            }
            
            return "<span class='badge bg-success'><i class='bi bi-check-circle'></i> Active</span>";
        }
    }
}

