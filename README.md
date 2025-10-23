# 🚴 Bike Ta Bai - Eco-Friendly Bike Rental Platform

A comprehensive two-sided marketplace bike rental web application built with ASP.NET Core 8.0 Razor Pages and MySQL. The platform enables bike owners to list their bikes for rent and renters to book bikes for eco-friendly commuting.

## ✨ Features

### Core Features
- **User Authentication & Authorization**
  - Cookie-based authentication with role-based access
  - Multiple user types: Renter, Owner, Admin
  - Users can be both Renter AND Owner
  - Password hashing with BCrypt
  - Session management (20-minute timeout)

- **Bike Management (Owner)**
  - List bikes with multiple images
  - Set hourly and daily rates
  - Track bike availability status
  - View earnings and booking history
  - Edit/delete bike listings

- **Bike Browsing & Search (Renter)**
  - Advanced filtering (type, location, price)
  - Sort by price, rating, or date
  - Interactive bike details with image gallery
  - Owner ratings and reviews
  - Real-time pricing calculator

- **Booking System**
  - Date/time range selection
  - Automatic pricing calculation
  - Multiple payment methods (Wallet, GCash, QRPH, Cash)
  - Booking status tracking
  - Cancellation with refund policy

- **Payment System**
  - Wallet integration
  - Simulated GCash/QRPH payments
  - Automatic earnings distribution to owners
  - Service fee deduction (10%)
  - Payment history tracking

- **Digital Wallet**
  - Load wallet via GCash/QRPH
  - Pay for rentals
  - Receive refunds and earnings
  - Transaction history
  - Withdrawal functionality

- **Rating & Review System**
  - Two-way ratings (Renter ↔ Owner)
  - 5-star rating system
  - Written reviews
  - Average rating calculations
  - Verified booking badges

- **Loyalty Points System**
  - Points for on-time returns (+10)
  - Eco-commute bonus (+1 per km)
  - First rental bonus (+50)
  - Long-term rental bonus (+20)
  - 5-star rating bonus (+5)
  - Points redemption for wallet credits

- **Dashboards**
  - Renter dashboard with active rentals and statistics
  - Owner dashboard with bike management and earnings
  - Eco-impact tracking (CO₂ saved)

- **Notifications**
  - In-app notification system
  - Real-time unread count
  - Booking confirmations
  - Payment alerts
  - Rating reminders

## 🛠️ Technology Stack

- **Framework**: ASP.NET Core 8.0 with Razor Pages
- **Database**: MySQL 8.0+
- **ORM**: Entity Framework Core
- **MySQL Provider**: Pomelo.EntityFrameworkCore.MySql
- **Authentication**: Cookie-based with role-based authorization
- **Frontend**: Bootstrap 5, jQuery
- **Password Hashing**: BCrypt.Net
- **Logging**: Serilog

## 📋 Prerequisites

- .NET 8.0 SDK
- MySQL 8.0 or higher
- Visual Studio 2022 / VS Code / JetBrains Rider

## 🚀 Setup Instructions

### 1. Clone the Repository

```bash
git clone <repository-url>
cd BiketaBai3.0
```

### 2. Configure Database Connection

Update the connection string in `appsettings.json` and `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=biketabai;User=root;Password=your_password;"
  }
}
```

### 3. Install Dependencies

```bash
dotnet restore
```

### 4. Create Database and Run Migrations

The application will automatically create the database and apply migrations on startup. However, you can also run migrations manually:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 5. Create Required Directories

```bash
mkdir -p wwwroot/uploads/bikes
mkdir -p wwwroot/images
mkdir -p logs
```

### 6. Run the Application

```bash
dotnet run
```

The application will be available at:
- HTTPS: `https://localhost:5001`
- HTTP: `http://localhost:5000`

## 📁 Project Structure

```
BiketaBai3.0/
├── Data/                          # Database context
│   └── BiketaBaiDbContext.cs
├── Models/                        # Entity models
│   ├── User.cs
│   ├── Bike.cs
│   ├── Booking.cs
│   ├── Payment.cs
│   ├── Wallet.cs
│   ├── Rating.cs
│   ├── Points.cs
│   └── ...
├── Services/                      # Business logic services
│   ├── BookingService.cs
│   ├── WalletService.cs
│   ├── PointsService.cs
│   ├── RatingService.cs
│   ├── PaymentService.cs
│   └── NotificationService.cs
├── Helpers/                       # Helper classes
│   ├── AuthHelper.cs
│   └── PasswordHelper.cs
├── Pages/                         # Razor Pages
│   ├── Index.cshtml               # Homepage
│   ├── Account/                   # Authentication
│   │   ├── Register.cshtml
│   │   ├── Login.cshtml
│   │   └── Logout.cshtml
│   ├── Bikes/                     # Bike browsing
│   │   ├── Browse.cshtml
│   │   └── Details.cshtml
│   ├── Owner/                     # Owner features
│   │   ├── MyBikes.cshtml
│   │   └── AddBike.cshtml
│   ├── Bookings/                  # Booking management
│   │   └── Payment.cshtml
│   ├── Wallet/                    # Wallet features
│   │   ├── Index.cshtml
│   │   └── Load.cshtml
│   ├── Points/                    # Points system
│   │   └── Index.cshtml
│   ├── Dashboard/                 # User dashboards
│   │   ├── Renter.cshtml
│   │   └── Owner.cshtml
│   └── Api/                       # API endpoints
│       ├── Wallet.cshtml
│       ├── Points.cshtml
│       └── Notifications.cshtml
├── wwwroot/                       # Static files
│   ├── css/
│   │   └── site.css              # Custom styles
│   ├── uploads/                   # User uploads
│   └── images/                    # Static images
├── appsettings.json              # Configuration
└── Program.cs                    # Application entry point
```

## 🔧 Configuration

### Application Settings

Edit `appsettings.json` to configure:

```json
{
  "AppSettings": {
    "ServiceFeePercentage": 10.0,
    "PointsConversionRate": 0.1,
    "SessionTimeoutMinutes": 20,
    "MinimumRentalHours": 1,
    "MaximumAdvanceBookingDays": 30,
    "CancellationFreeHours": 24,
    "CancellationPartialRefundPercentage": 50,
    "GoogleMapsApiKey": "YOUR_API_KEY"
  },
  "PointsRules": {
    "OnTimeReturn": 10,
    "EcoCommuteBonusPerKm": 1,
    "FirstRental": 50,
    "Referral": 100,
    "LongTermRental": 20,
    "HighlyRated": 5,
    "CompleteProfile": 20
  }
}
```

## 👥 Default User Types

1. **Renter** - Can browse and rent bikes
2. **Owner** - Can list bikes for rent
3. **Admin** - Full system access (manually set in database)

Users can register as both Renter and Owner simultaneously.

## 💳 Payment Methods

1. **Wallet** - Digital wallet system (recommended)
2. **GCash** - Simulated mobile payment
3. **QRPH** - Simulated QR payment
4. **Cash** - Direct payment to owner

## 🎯 Loyalty Points Rules

- **On-time Return**: +10 points
- **Eco-commute**: +1 point per km saved
- **First Rental**: +50 points (one-time)
- **Long-term Rental** (7+ days): +20 points
- **5-Star Rating**: +5 points
- **Complete Profile**: +20 points (one-time)
- **Referral**: +100 points

**Redemption**: 100 points = ₱10 wallet credit

## 📊 Database Schema

The application uses the following main tables:
- `users` - User accounts
- `bikes` - Bike listings
- `bike_types` - Bike categories
- `bike_images` - Bike photos
- `bookings` - Rental bookings
- `payments` - Payment transactions
- `wallets` - User wallets
- `credit_transactions` - Wallet transactions
- `ratings` - Reviews and ratings
- `points` - User points
- `points_history` - Points transactions
- `notifications` - User notifications

## 🔐 Security Features

- Password hashing with BCrypt
- HTTPS enforcement
- Secure cookies (HttpOnly, Secure, SameSite)
- CSRF protection
- SQL injection prevention (EF Core parameterized queries)
- XSS protection
- Role-based authorization
- Session timeout (20 minutes)

## 🎨 UI/UX Features

- Responsive design (mobile-first)
- Eco-friendly color theme (greens, blues)
- Bootstrap 5 components
- Interactive bike image carousels
- Real-time pricing calculator
- Live wallet/points balance updates
- Notification badges
- Loading animations

## 📝 Additional Pages Needed (Future Enhancement)

- Admin Dashboard
- Booking Details page
- Booking Confirmation page
- Edit Bike page
- User Profile page
- Notifications page
- About page
- Contact page
- Privacy Policy page

## 🐛 Troubleshooting

### Database Connection Issues
- Ensure MySQL server is running
- Check connection string credentials
- Verify port number (default: 3306)

### Migration Issues
```bash
dotnet ef database drop --force
dotnet ef migrations remove
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### File Upload Issues
- Ensure `wwwroot/uploads/bikes` directory exists
- Check directory permissions

## 📄 License

This project is for educational purposes.

## 👨‍💻 Development

Built with ❤️ using ASP.NET Core 8.0 and MySQL

---

**Note**: This is a demonstration project. For production use, implement:
- Real payment gateway integration
- Email service for notifications
- Google Maps API integration
- Image optimization and CDN
- Advanced security measures
- Comprehensive error handling
- Unit and integration tests

