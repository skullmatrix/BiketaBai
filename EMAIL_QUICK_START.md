# 📧 Email Verification - Quick Start Guide

## 🚀 5-Minute Setup

### Step 1: Enable Gmail App Password (2 minutes)

1. Go to: https://myaccount.google.com/security
2. Enable "2-Step Verification" if not already enabled
3. Go to: https://myaccount.google.com/apppasswords
4. Create new app password for "Mail"
5. Copy the 16-character password (remove spaces)

### Step 2: Configure App (1 minute)

Edit `appsettings.json`:

```json
"EmailSettings": {
  "SenderName": "Bike Ta Bai",
  "SenderEmail": "your-email@gmail.com",      ← YOUR GMAIL
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": "587",
  "SmtpPassword": "abcdefghijklmnop"          ← YOUR APP PASSWORD (no spaces)
}
```

### Step 3: Update Database (1 minute)

```bash
cd /Users/macbookpro/Documents/BiketaBai3.0
dotnet ef migrations add AddEmailVerificationFields
dotnet ef database update
```

### Step 4: Install Package & Run (1 minute)

```bash
dotnet restore
dotnet run
```

### Step 5: Test (1 minute)

1. Open: http://localhost:5000/Account/Register
2. Register with your real email
3. Check inbox/spam for verification email
4. Click verification link
5. Login!

---

## 🎯 What Was Implemented

### New Features:
- ✅ Email verification on registration
- ✅ Verification email with styled HTML template
- ✅ 24-hour token expiration
- ✅ Prevents login until verified
- ✅ Welcome email after verification
- ✅ Points awarded on email verification

### New Files Created:
1. `Services/EmailService.cs` - Email sending service
2. `Pages/Account/RegistrationSuccess.cshtml` - Success page
3. `Pages/Account/VerifyEmail.cshtml` - Verification handler
4. `EMAIL_SETUP_GUIDE.md` - Detailed guide (this file)

### Modified Files:
1. `BiketaBai.csproj` - Added MailKit package
2. `Models/User.cs` - Added verification token fields
3. `Program.cs` - Registered EmailService
4. `appsettings.json` - Added EmailSettings
5. `Pages/Account/Register.cshtml.cs` - Sends verification email
6. `Pages/Account/Login.cshtml.cs` - Checks email verified

---

## 🔑 Important Notes

### Security:
- ⚠️ **Never use your Gmail password** - only use App Password
- ⚠️ **Don't commit appsettings.json** with real credentials
- ⚠️ App Password format: `abcdefghijklmnop` (16 chars, no spaces)

### Testing:
- Use your real email for testing
- Check spam folder if email doesn't arrive
- Token expires in 24 hours
- Can't login until email is verified

---

## 🐛 Common Issues

| Issue | Solution |
|-------|----------|
| "Authentication failed" | Check App Password (remove spaces!) |
| "Table doesn't exist" | Run `dotnet ef database update` |
| "Email not received" | Check spam folder, wait a few minutes |
| "Connection refused" | Check port 587 isn't blocked by firewall |

---

## 📋 Quick Checklist

Before testing:
- [ ] 2-Step Verification enabled on Gmail
- [ ] App Password generated
- [ ] appsettings.json updated with email & password
- [ ] Database migration run
- [ ] Package restored (`dotnet restore`)
- [ ] App running (`dotnet run`)

---

## 📧 Email Templates

### Verification Email
- Professional HTML design
- Big green button with link
- Eco-friendly theme (green colors)
- 24-hour expiry warning

### Welcome Email
- Sent after successful verification
- Highlights platform features
- Next steps for user
- Call-to-action buttons

---

## 🎓 How It Works

```
1. User Registers
   ↓
2. System generates unique token
   ↓
3. Token saved in database (expires in 24h)
   ↓
4. Verification email sent with link
   ↓
5. User clicks link in email
   ↓
6. System validates token
   ↓
7. Email marked as verified
   ↓
8. User can now login
```

---

## 💡 Pro Tips

1. **Test with real email** - Use your own email for testing
2. **Check spam** - Verification emails may go to spam initially
3. **Token expires** - Has 24-hour expiration for security
4. **Points reward** - Users get 20 points after verification
5. **Welcome email** - Automatic welcome email sent after verification

---

## 🔗 Full Documentation

For complete details, see: `EMAIL_SETUP_GUIDE.md`

For troubleshooting, check: `logs/biketabai-*.txt`

---

## ✅ You're Done!

Email verification is now active! Users must verify their email before they can login.

**Next Steps:**
1. Test the registration flow
2. Verify email works
3. Update production settings
4. Consider adding password reset feature

---

Happy Coding! 🚴💨

