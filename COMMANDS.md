# 🚴 Bike Ta Bai - Quick Command Reference

## Database Commands

### Create Migration
```bash
dotnet ef migrations add MigrationName
```

### Apply Migrations
```bash
dotnet ef database update
```

### Remove Last Migration
```bash
dotnet ef migrations remove
```

### Drop Database
```bash
dotnet ef database drop --force
```

### Recreate Database from Scratch
```bash
dotnet ef database drop --force
dotnet ef migrations remove
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Build & Run Commands

### Restore Packages
```bash
dotnet restore
```

### Build Project
```bash
dotnet build
```

### Run Application
```bash
dotnet run
```

### Run with Auto-reload (Watch Mode)
```bash
dotnet watch run
```

### Run in Production Mode
```bash
dotnet run --environment Production
```

## Database Connection Test

### Test MySQL Connection
```bash
mysql -u root -p
```

```sql
USE biketabai;
SHOW TABLES;
SELECT COUNT(*) FROM users;
SELECT COUNT(*) FROM bikes;
SELECT COUNT(*) FROM bookings;
```

## Useful SQL Queries

### Make a User an Admin
```sql
UPDATE users SET is_admin = 1 WHERE email = 'admin@example.com';
```

### Check Recent Registrations
```sql
SELECT user_id, full_name, email, is_renter, is_owner, created_at 
FROM users 
ORDER BY created_at DESC 
LIMIT 10;
```

### View All Bike Listings
```sql
SELECT b.bike_id, b.brand, b.model, bt.type_name, b.hourly_rate, 
       u.full_name as owner, ast.status_name
FROM bikes b
JOIN bike_types bt ON b.bike_type_id = bt.bike_type_id
JOIN users u ON b.owner_id = u.user_id
JOIN availability_statuses ast ON b.availability_status_id = ast.status_id;
```

### View Recent Bookings
```sql
SELECT b.booking_id, r.full_name as renter, o.full_name as owner,
       bk.brand, bk.model, b.total_amount, bs.status_name
FROM bookings b
JOIN users r ON b.renter_id = r.user_id
JOIN bikes bk ON b.bike_id = bk.bike_id
JOIN users o ON bk.owner_id = o.user_id
JOIN booking_statuses bs ON b.booking_status_id = bs.status_id
ORDER BY b.created_at DESC
LIMIT 10;
```

### Check Wallet Balances
```sql
SELECT u.full_name, u.email, w.balance
FROM wallets w
JOIN users u ON w.user_id = u.user_id
ORDER BY w.balance DESC;
```

### View Points Leaderboard
```sql
SELECT u.full_name, u.email, p.total_points
FROM points p
JOIN users u ON p.user_id = u.user_id
ORDER BY p.total_points DESC
LIMIT 10;
```

## Package Management

### Add Package
```bash
dotnet add package PackageName
```

### Remove Package
```bash
dotnet remove package PackageName
```

### List Packages
```bash
dotnet list package
```

### Update All Packages
```bash
dotnet list package --outdated
dotnet add package PackageName --version X.X.X
```

## Clean & Reset

### Clean Build Artifacts
```bash
dotnet clean
```

### Remove bin and obj directories
```bash
rm -rf bin obj
```

### Clear NuGet Cache
```bash
dotnet nuget locals all --clear
```

## Development Tools

### Install EF Core Tools (if not installed)
```bash
dotnet tool install --global dotnet-ef
```

### Update EF Core Tools
```bash
dotnet tool update --global dotnet-ef
```

### Check EF Core Version
```bash
dotnet ef --version
```

## Port Management

### Kill Process on Port (macOS/Linux)
```bash
lsof -ti:5001 | xargs kill -9
```

### Kill Process on Port (Windows)
```bash
netstat -ano | findstr :5001
taskkill /PID [PID] /F
```

## Logs

### View Application Logs
```bash
tail -f logs/biketabai-*.txt
```

### Clear Logs
```bash
rm logs/*
```

## Testing URLs

After running `dotnet run`, access:

- **Home**: https://localhost:5001
- **Browse Bikes**: https://localhost:5001/Bikes/Browse
- **Register**: https://localhost:5001/Account/Register
- **Login**: https://localhost:5001/Account/Login
- **Renter Dashboard**: https://localhost:5001/Dashboard/Renter
- **Owner Dashboard**: https://localhost:5001/Dashboard/Owner
- **Admin Dashboard**: https://localhost:5001/Admin/Dashboard
- **Wallet**: https://localhost:5001/Wallet/Index
- **Points**: https://localhost:5001/Points/Index

## Troubleshooting Commands

### Check .NET Version
```bash
dotnet --version
```

### Check MySQL Version
```bash
mysql --version
```

### Test MySQL Server
```bash
mysqladmin -u root -p ping
```

### View Running Processes
```bash
ps aux | grep dotnet
```

### Check Port Usage
```bash
netstat -an | grep 5001
```

## Git Commands (if using version control)

### Initialize Repository
```bash
git init
git add .
git commit -m "Initial commit: Complete Bike Ta Bai platform"
```

### Create .gitignore (already provided)
The `.gitignore` file is already in the project root.

---

**Quick Start:**
```bash
cd BiketaBai3.0
dotnet restore
dotnet run
```

Then open: https://localhost:5001

