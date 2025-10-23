# 🚀 Quick Setup Guide - Bike Ta Bai

## Step-by-Step Setup

### 1. Install Prerequisites

#### Install .NET 8.0 SDK
- Download from: https://dotnet.microsoft.com/download/dotnet/8.0
- Verify installation: `dotnet --version`

#### Install MySQL
- Download from: https://dev.mysql.com/downloads/mysql/
- Or use Docker: `docker run -d -p 3306:3306 --name mysql -e MYSQL_ROOT_PASSWORD=your_password mysql:8.0`

### 2. Configure Database

#### Create MySQL Database
```sql
CREATE DATABASE biketabai CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

#### Update Connection String
Edit `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=biketabai;User=root;Password=YOUR_MYSQL_PASSWORD;"
}
```

### 3. Setup Project

```bash
# Navigate to project directory
cd BiketaBai3.0

# Restore NuGet packages
dotnet restore

# Create upload directories
mkdir -p wwwroot/uploads/bikes
mkdir -p wwwroot/images
mkdir -p logs

# Build the project
dotnet build
```

### 4. Database Migration

The application will automatically run migrations on startup. Alternatively:

```bash
# Add migration (if needed)
dotnet ef migrations add InitialCreate

# Update database
dotnet ef database update
```

### 5. Run the Application

```bash
dotnet run
```

Or with watch mode (auto-reload on changes):
```bash
dotnet watch run
```

### 6. Access the Application

Open your browser and navigate to:
- **HTTPS**: https://localhost:5001
- **HTTP**: http://localhost:5000

### 7. Create First Users

#### Register as Renter and Owner
1. Go to Register page
2. Fill in your details
3. Check both "Rent bikes" and "List my bikes"
4. Submit registration

You'll receive 20 points for completing your profile!

### 8. Test the Platform

#### As a Renter:
1. Browse available bikes
2. Load your wallet (Wallet → Load Wallet)
3. Book a bike
4. Complete payment
5. Manage your bookings in Renter Dashboard

#### As an Owner:
1. Go to Owner Dashboard
2. Click "Add New Bike"
3. Fill in bike details and upload images
4. Set your pricing
5. View bookings and earnings

## 🔧 Troubleshooting

### Port Already in Use
```bash
# Change ports in Properties/launchSettings.json
# Or kill the process using the port
lsof -ti:5001 | xargs kill -9  # macOS/Linux
```

### Database Connection Failed
- Verify MySQL is running: `mysql -u root -p`
- Check connection string credentials
- Ensure database `biketabai` exists

### Migration Errors
```bash
# Drop and recreate database
dotnet ef database drop --force
dotnet ef database update
```

### File Upload Issues
```bash
# Ensure directory exists and has permissions
chmod 755 wwwroot/uploads/bikes
```

## 📦 Package Installation Issues

If you encounter package restore issues:

```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore

# If using Visual Studio, restore via Tools → NuGet Package Manager → Package Manager Console
Update-Package -reinstall
```

## 🎯 Next Steps

1. **Customize Configuration**
   - Update service fee percentage
   - Adjust points rules
   - Configure session timeout

2. **Add Test Data**
   - Create multiple user accounts
   - List several bikes
   - Create test bookings

3. **Explore Features**
   - Test wallet loading
   - Try booking flow
   - Rate and review
   - Redeem points

4. **Admin Access**
   - Manually set `is_admin = 1` in `users` table for admin features

## 🚨 Common Issues

### Issue: Cannot connect to database
**Solution**: Start MySQL service
```bash
# macOS
brew services start mysql

# Linux
sudo systemctl start mysql

# Windows
net start MySQL80
```

### Issue: Migrations not applying
**Solution**: Ensure EF Core tools are installed
```bash
dotnet tool install --global dotnet-ef
dotnet ef database update
```

### Issue: HTTPS certificate errors
**Solution**: Trust the development certificate
```bash
dotnet dev-certs https --trust
```

## 📞 Support

For issues or questions:
1. Check the main README.md
2. Review error logs in `logs/` directory
3. Check application console output

---

Happy coding! 🚴‍♂️

