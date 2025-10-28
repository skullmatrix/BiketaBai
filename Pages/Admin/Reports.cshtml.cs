using BiketaBai.Data;
using BiketaBai.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BiketaBai.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ReportsModel : PageModel
    {
        private readonly BiketaBaiDbContext _context;

        public ReportsModel(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public List<Report> Reports { get; set; } = new List<Report>();
        public List<Report> FilteredReports { get; set; } = new List<Report>();
        
        [BindProperty(SupportsGet = true)]
        public string? SearchQuery { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? TypeFilter { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }
        
        [BindProperty(SupportsGet = true)]
        public string? PriorityFilter { get; set; }
        
        public int TotalReports { get; set; }
        public int PendingReports { get; set; }
        public int InProgressReports { get; set; }
        public int ResolvedReports { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }
        
        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Reports = await _context.Reports
                .Include(r => r.Reporter)
                .Include(r => r.ReportedUser)
                .Include(r => r.ReportedBike)
                .Include(r => r.Booking)
                .Include(r => r.AssignedAdmin)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Calculate statistics
            TotalReports = Reports.Count;
            PendingReports = Reports.Count(r => r.Status == "Pending");
            InProgressReports = Reports.Count(r => r.Status == "Assigned" || r.Status == "In Progress");
            ResolvedReports = Reports.Count(r => r.Status == "Resolved" || r.Status == "Closed");

            FilteredReports = ApplyFilters(Reports);

            return Page();
        }

        private List<Report> ApplyFilters(List<Report> reports)
        {
            var filtered = reports.AsEnumerable();

            // Search
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                filtered = filtered.Where(r =>
                    r.Subject.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    r.Description.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    r.Reporter.FullName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            }

            // Type Filter
            if (!string.IsNullOrWhiteSpace(TypeFilter) && TypeFilter != "All")
            {
                filtered = filtered.Where(r => r.ReportType == TypeFilter);
            }

            // Status Filter
            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
            {
                filtered = filtered.Where(r => r.Status == StatusFilter);
            }

            // Priority Filter
            if (!string.IsNullOrWhiteSpace(PriorityFilter) && PriorityFilter != "All")
            {
                filtered = filtered.Where(r => r.Priority == PriorityFilter);
            }

            return filtered.ToList();
        }

        public async Task<IActionResult> OnPostAssignAsync(int reportId)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null)
            {
                ErrorMessage = "Report not found.";
                return RedirectToPage();
            }

            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            
            report.AssignedToAdminId = adminId;
            report.Status = "Assigned";
            report.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            SuccessMessage = "Report assigned to you successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int reportId, string newStatus)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null)
            {
                ErrorMessage = "Report not found.";
                return RedirectToPage();
            }

            report.Status = newStatus;
            report.UpdatedAt = DateTime.UtcNow;

            if (newStatus == "Resolved" || newStatus == "Closed")
            {
                report.ResolvedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            SuccessMessage = $"Report status updated to {newStatus}.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdatePriorityAsync(int reportId, string newPriority)
        {
            var report = await _context.Reports.FindAsync(reportId);
            if (report == null)
            {
                ErrorMessage = "Report not found.";
                return RedirectToPage();
            }

            report.Priority = newPriority;
            report.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            SuccessMessage = $"Report priority updated to {newPriority}.";
            return RedirectToPage();
        }
    }
}

