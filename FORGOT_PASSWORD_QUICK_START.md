# 🚀 Forgot Password - Quick Start Guide

## ⚡ Quick Setup (Already Done!)

The Forgot Password feature is **already installed and ready to use!** Here's what was set up:

✅ Database columns added to `users` table  
✅ Password reset email templates created  
✅ ForgotPassword and ResetPassword pages created  
✅ Email service configured  
✅ Migration applied to database  

---

## 🎯 Test It Right Now!

### 1️⃣ Start the Application

```bash
cd /Users/macbookpro/Documents/BiketaBai3.0
dotnet watch run
```

### 2️⃣ Test the Flow

1. **Go to Login Page:**
   ```
   http://localhost:5000/Account/Login
   ```

2. **Click "Forgot password?" link**

3. **Enter a registered email address**
   - Use an email from your existing test users
   - Or register a new account first

4. **Check Your Email**
   - Open your Gmail inbox
   - Look for "Reset Your Password - Bike Ta Bai"
   - If not in inbox, check Spam folder

5. **Click Reset Link**
   - Click the green "Reset Password" button
   - Or copy/paste the link

6. **Enter New Password**
   - Must be at least 8 characters
   - Must have uppercase, lowercase, and number
   - Examples: `Password123`, `BikeRent99`

7. **Login with New Password**
   - You'll be redirected to login page
   - Login with your new password

---

## 📧 Email Configuration

Your email is already configured in `appsettings.json`:

```json
"EmailSettings": {
  "SenderName": "Bike Ta Bai",
  "SenderEmail": "your-email@gmail.com",
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": "587",
  "SmtpPassword": "your-app-password-here"
}
```

**⚠️ IMPORTANT:** Make sure you've set up your Gmail App Password!

If you haven't already, follow `EMAIL_SETUP_GUIDE.md` to:
1. Enable 2-Step Verification on your Google account
2. Generate an App Password
3. Update `appsettings.json` with your credentials

---

## 🔑 Key URLs

| Page | URL |
|------|-----|
| **Login** | `http://localhost:5000/Account/Login` |
| **Forgot Password** | `http://localhost:5000/Account/ForgotPassword` |
| **Reset Password** | `http://localhost:5000/Account/ResetPassword?token=...&email=...` |

---

## 🧪 Quick Test Commands

### Test 1: Request Password Reset
```bash
# Start app
dotnet watch run

# In browser:
# 1. Go to http://localhost:5000/Account/ForgotPassword
# 2. Enter: test@example.com (or your test user email)
# 3. Click "Send Reset Link"
# 4. Check email inbox
```

### Test 2: Verify Email Sends
```bash
# Check app logs
tail -f logs/*.log

# Look for email sending confirmation
```

### Test 3: Complete Password Reset
```bash
# 1. Click link in email
# 2. Enter new password: Password123
# 3. Confirm password: Password123
# 4. Click "Reset Password"
# 5. Login at http://localhost:5000/Account/Login
```

---

## ⏱️ Important Timings

- **Reset Link Expires:** 1 hour after generation
- **One-Time Use:** Link cannot be reused after password reset

---

## 🎨 What Users Will See

### Forgot Password Page
- Simple form with email input
- Green "Send Reset Link" button
- Links back to Login and Register

### Password Reset Email
- Professional green-themed design
- Big "Reset Password" button
- Security warnings
- 1-hour expiration notice

### Reset Password Page
- **If Link Valid:**
  - New password input
  - Confirm password input
  - "Reset Password" button

- **If Link Expired/Invalid:**
  - Red error icon
  - Clear error message
  - "Request New Reset Link" button

### Confirmation Email
- Success message
- Security warning (if you didn't change it)
- "Login to Your Account" button

---

## 🐛 Quick Troubleshooting

### Problem: Email not arriving

**Quick Fixes:**
```bash
# 1. Check spam folder
# 2. Verify email in appsettings.json
grep "SenderEmail" appsettings.json

# 3. Check app logs
tail -20 logs/*.log | grep -i email
```

### Problem: "Invalid or Expired Link"

**Quick Fixes:**
- Link expires after 1 hour - request new one
- Link is one-time use - request new one if already used
- Make sure you copied the complete URL

### Problem: Password not accepted

**Requirements:**
- ✅ Minimum 8 characters
- ✅ At least 1 uppercase letter (A-Z)
- ✅ At least 1 lowercase letter (a-z)
- ✅ At least 1 number (0-9)

**Valid:** `Password123`, `BikeRent99`, `MyPass2024`  
**Invalid:** `password`, `PASSWORD`, `Pass123` (too short)

---

## 📊 Feature Status

| Component | Status |
|-----------|--------|
| Database Schema | ✅ Ready |
| Email Templates | ✅ Ready |
| Forgot Password Page | ✅ Ready |
| Reset Password Page | ✅ Ready |
| Email Sending | ✅ Ready |
| Password Validation | ✅ Ready |
| Security Measures | ✅ Ready |
| Migration Applied | ✅ Ready |

---

## 🔗 Pages Flow

```
Login Page
    ↓ (click "Forgot password?")
Forgot Password Page
    ↓ (enter email, submit)
Email Sent Confirmation
    ↓ (check email)
Password Reset Email
    ↓ (click link)
Reset Password Page
    ↓ (enter new password)
Login Page (with success message)
    ↓ (login with new password)
Dashboard
```

---

## 📋 Security Checklist

✅ Tokens expire after 1 hour  
✅ One-time use tokens  
✅ Email enumeration protection  
✅ Strong password enforcement  
✅ Confirmation email sent  
✅ HTTPS ready (in production)  
✅ Tokens cleared after use  
✅ Proper error messages  

---

## 🎉 You're All Set!

The Forgot Password feature is **fully functional** and ready to use!

Try it out:
1. Start app: `dotnet watch run`
2. Go to: `http://localhost:5000/Account/Login`
3. Click: "Forgot password?"
4. Test the complete flow!

---

For detailed information, see:
- **FORGOT_PASSWORD_GUIDE.md** - Complete documentation
- **EMAIL_SETUP_GUIDE.md** - Gmail configuration
- **EMAIL_QUICK_START.md** - Email system setup

