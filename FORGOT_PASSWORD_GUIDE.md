# 🔐 Forgot Password Feature - Complete Guide

## ✅ Feature Overview

The **Forgot Password** feature allows users who have forgotten their password to securely reset it by receiving a password reset link via email. This feature integrates seamlessly with your existing email verification system.

---

## 🎯 How It Works

### User Flow:
1. **User clicks "Forgot password?" on Login page**
2. **User enters their email address**
3. **System generates secure reset token (valid for 1 hour)**
4. **System sends password reset email with link**
5. **User clicks link in email**
6. **User enters new password**
7. **Password is updated**
8. **User receives confirmation email**
9. **User can login with new password**

---

## 🔧 What Was Implemented

### 1. Database Changes
- Added `password_reset_token` column to `users` table
- Added `password_reset_token_expires` column to `users` table

### 2. Email Service Updates
- **`SendPasswordResetEmailAsync()`** - Sends password reset link
- **`SendPasswordChangedNotificationAsync()`** - Confirms password was changed

### 3. New Pages Created

#### **ForgotPassword Page** (`/Account/ForgotPassword`)
- User enters their email
- Generates reset token
- Sends email with reset link
- Uses security best practice: Always shows success message even if email doesn't exist (prevents email enumeration attacks)

#### **ResetPassword Page** (`/Account/ResetPassword`)
- Validates reset token and expiration
- User enters new password
- Updates password in database
- Clears reset token
- Sends confirmation email

---

## 🚀 How to Use

### For Users:

1. **Initiate Password Reset:**
   - Go to: `http://localhost:5000/Account/Login`
   - Click "Forgot password?" link
   - Enter your email address
   - Click "Send Reset Link"

2. **Check Your Email:**
   - Open your email inbox
   - Look for email from "Bike Ta Bai"
   - Subject: "Reset Your Password - Bike Ta Bai"
   - Check spam folder if not in inbox

3. **Reset Your Password:**
   - Click "Reset Password" button in email
   - Or copy/paste the reset link into your browser
   - Enter your new password (minimum 8 characters, uppercase, lowercase, number)
   - Confirm your new password
   - Click "Reset Password"

4. **Login:**
   - You'll be redirected to the login page
   - Login with your new password

---

## 🔒 Security Features

### ✅ Implemented Security Measures:

1. **Token Expiration:**
   - Reset tokens expire after **1 hour**
   - Prevents old links from being used

2. **One-Time Use Tokens:**
   - Token is cleared after successful password reset
   - Cannot reuse the same reset link

3. **Email Enumeration Protection:**
   - Always shows same message whether email exists or not
   - Prevents attackers from discovering valid email addresses

4. **Strong Password Enforcement:**
   - Minimum 8 characters
   - Must include uppercase letter
   - Must include lowercase letter
   - Must include number

5. **Confirmation Email:**
   - User receives email when password is changed
   - Alerts them if someone else changed their password

6. **HTTPS Links:**
   - Reset links use HTTPS in production
   - Prevents man-in-the-middle attacks

---

## 📧 Email Templates

### Password Reset Email
- **Subject:** Reset Your Password - Bike Ta Bai
- **Contains:**
  - Personalized greeting with user's name
  - Clear "Reset Password" button
  - Plain text link (for email clients that don't support buttons)
  - Security warnings (link expires in 1 hour)
  - Instructions to ignore if they didn't request reset

### Password Changed Confirmation Email
- **Subject:** Password Changed Successfully - Bike Ta Bai
- **Contains:**
  - Confirmation that password was changed
  - Link to login page
  - Warning to contact support if they didn't make the change

---

## 🧪 Testing the Feature

### Test Scenario 1: Successful Password Reset

```bash
# Step 1: Make sure app is running
cd /Users/macbookpro/Documents/BiketaBai3.0
dotnet watch run

# Step 2: Open browser
http://localhost:5000/Account/Login

# Step 3: Click "Forgot password?"

# Step 4: Enter a registered email address

# Step 5: Check email inbox for reset link

# Step 6: Click reset link or copy/paste into browser

# Step 7: Enter new password

# Step 8: Login with new password
```

### Test Scenario 2: Expired Token

```bash
# Step 1: Request password reset
# Step 2: Wait more than 1 hour
# Step 3: Try to use the reset link
# Expected: "This password reset link has expired" error
```

### Test Scenario 3: Invalid Token

```bash
# Step 1: Request password reset
# Step 2: Modify the token in the URL
# Expected: "Invalid password reset link" error
```

### Test Scenario 4: Non-existent Email

```bash
# Step 1: Go to Forgot Password page
# Step 2: Enter email that doesn't exist in database
# Expected: Same success message (security feature)
# Expected: No email is actually sent
```

---

## 🐛 Troubleshooting

### Problem: Not Receiving Reset Email

**Solutions:**
1. Check spam/junk folder
2. Verify email settings in `appsettings.json`:
   ```json
   "EmailSettings": {
     "SenderEmail": "your-email@gmail.com",
     "SmtpPassword": "your-app-password-here"
   }
   ```
3. Check application logs in `logs/` folder
4. Verify Gmail App Password is correct
5. Test email service with a simple test

**Test Email Service:**
```csharp
// Add this to a test page or controller
await _emailService.SendPasswordResetEmailAsync(
    "your-test-email@gmail.com",
    "Test User",
    "http://test.com/reset"
);
```

### Problem: "Invalid or Expired Link" Error

**Solutions:**
1. Check if link was used more than 1 hour ago
2. Request a new password reset link
3. Make sure you're using the complete link from email
4. Check if password was already reset (tokens are one-time use)

### Problem: Weak Password Error

**Solutions:**
1. Make sure password is at least 8 characters
2. Include at least one uppercase letter (A-Z)
3. Include at least one lowercase letter (a-z)
4. Include at least one number (0-9)

**Valid Examples:**
- `Password123`
- `MySecure2024`
- `BikeRental99`

**Invalid Examples:**
- `password` (no uppercase, no number)
- `PASSWORD123` (no lowercase)
- `Pass123` (too short)

---

## 📁 File Changes

### Modified Files:
1. **Models/User.cs**
   - Added `PasswordResetToken` property
   - Added `PasswordResetTokenExpires` property

2. **Services/EmailService.cs**
   - Added `SendPasswordResetEmailAsync()` method
   - Added `SendPasswordChangedNotificationAsync()` method

### New Files:
1. **Pages/Account/ForgotPassword.cshtml** - Forgot password form
2. **Pages/Account/ForgotPassword.cshtml.cs** - Forgot password logic
3. **Pages/Account/ResetPassword.cshtml** - Password reset form
4. **Pages/Account/ResetPassword.cshtml.cs** - Password reset logic

### Database Migration:
- **Migration Name:** `AddPasswordResetTokens`
- **Creates:** `password_reset_token` and `password_reset_token_expires` columns

---

## 🎨 UI Design

### Forgot Password Page
- Clean, centered form
- Email input field
- "Send Reset Link" button
- Link back to login page
- Link to registration page
- Info box about checking spam folder

### Reset Password Page
- **Valid Token:**
  - New password input
  - Confirm password input
  - "Reset Password" button
  - Password strength requirements

- **Invalid/Expired Token:**
  - Error icon
  - Clear error message
  - "Request New Reset Link" button
  - "Back to Login" button

---

## 🔗 Integration with Existing Features

### Works With:
✅ **Email Verification System** - Uses same EmailService
✅ **Login System** - Users can login after password reset
✅ **Password Hashing** - Uses same PasswordHelper
✅ **User Authentication** - Maintains all security measures
✅ **Notification System** - Could be extended to notify on password change

---

## 🚀 Next Steps (Optional Enhancements)

### Possible Improvements:

1. **Rate Limiting:**
   - Limit password reset requests per email (e.g., 3 per hour)
   - Prevents abuse

2. **Password History:**
   - Store hash of previous passwords
   - Prevent reuse of old passwords

3. **Two-Factor Authentication:**
   - Send verification code in addition to email link
   - Extra security layer

4. **Activity Log:**
   - Track password reset attempts
   - Admin can review security events

5. **SMS Reset:**
   - Alternative to email
   - Send reset code via SMS

---

## ✅ Feature Checklist

- [x] Database schema updated
- [x] Password reset token generation
- [x] Token expiration (1 hour)
- [x] Email templates created
- [x] Forgot Password page
- [x] Reset Password page
- [x] Email sending functionality
- [x] Password validation
- [x] Token validation
- [x] Confirmation email
- [x] Security best practices
- [x] Error handling
- [x] User-friendly UI
- [x] Migration applied

---

## 📞 Support

If you encounter any issues:

1. Check the **Troubleshooting** section above
2. Review application logs in `logs/` folder
3. Verify email configuration in `appsettings.json`
4. Check MySQL database tables have been updated
5. Ensure app is running on correct port

---

**🎉 Your Forgot Password feature is now fully functional!**

Users can now securely reset their passwords by receiving a link via email. The feature includes proper security measures, email notifications, and a user-friendly interface.

