using BiketaBai.Models;
using BiketaBai.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BiketaBai.Data
{
    public class DatabaseSeeder
    {
        private readonly BiketaBaiDbContext _context;

        public DatabaseSeeder(BiketaBaiDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync()
        {
            try
            {
                // Seed Admin User
                await SeedAdminUserAsync();

                // You can add more seeding methods here
                // await SeedBikeTypesAsync();
                // await SeedAvailabilityStatusesAsync();
                // etc.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error seeding database: {ex.Message}");
                throw;
            }
        }

        private async Task SeedAdminUserAsync()
        {
            // Check if admin user already exists
            var adminEmail = "admin@biketabai.org";
            var adminExists = await _context.Users
                .AnyAsync(u => u.Email == adminEmail);

            if (!adminExists)
            {
                Console.WriteLine("Creating admin user...");

                // Create admin user (Super Admin - no renter/owner roles)
                var adminUser = new User
                {
                    FullName = "System Administrator",
                    Email = adminEmail,
                    PasswordHash = PasswordHelper.HashPassword("BiketaBai@2024"),
                    Phone = "+639163436964",
                    Address = "BiketaBai Headquarters, Cebu City, Philippines",
                    
                    // Super Admin - only admin role
                    IsAdmin = true,
                    IsOwner = false,
                    IsRenter = false,
                    
                    // Auto-verify email (no owner verification needed since not an owner)
                    IsEmailVerified = true,
                    IsVerifiedOwner = false,
                    VerificationStatus = "N/A",
                    
                    // Set profile photo to a default admin avatar
                    ProfilePhotoUrl = "/images/default-admin.png",
                    
                    // Activity tracking
                    LastLoginAt = DateTime.UtcNow,
                    LoginCount = 0,
                    
                    // Timestamps
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(adminUser);
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Admin user created with email: {adminEmail}");

                // Create wallet for admin
                var wallet = new Wallet
                {
                    UserId = adminUser.UserId,
                    Balance = 10000.00m, // Give admin some initial balance
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Wallets.Add(wallet);

                // Create points for admin
                var points = new Points
                {
                    UserId = adminUser.UserId,
                    TotalPoints = 1000, // Give admin some initial points
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Points.Add(points);

                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Super Admin account initialized");
                Console.WriteLine($"   Email: {adminEmail}");
                Console.WriteLine($"   Password: BiketaBai@2024");
                Console.WriteLine($"   Role: Super Admin (Account Management, Bike Management, Owner Verification)");
                Console.WriteLine($"   Wallet Balance: ₱10,000.00");
                Console.WriteLine($"   Points: 1,000");
            }
            else
            {
                Console.WriteLine($"ℹ️  Admin user already exists: {adminEmail}");
            }
        }
    }
}

