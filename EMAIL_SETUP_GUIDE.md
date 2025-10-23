# 📧 Email Verification Setup Guide

## Complete Step-by-Step Guide to Enable Gmail Email Verification

This guide will walk you through setting up Gmail SMTP for sending verification emails in your Bike Ta Bai application.

---

## 🎯 Overview

The email verification system:
1. ✅ Sends verification email when user registers
2. ✅ User clicks link in email to verify
3. ✅ Account is activated after verification
4. ✅ User can then login
5. ✅ Sends welcome email after verification

---

## 📋 Prerequisites

- A Gmail account (or Google Workspace account)
- Access to Google Account settings

---

## 🔧 Step 1: Enable 2-Step Verification on Your Gmail

**Why:** Google requires 2-step verification to generate App Passwords for security.

### Instructions:

1. **Go to Google Account Security**
   - Visit: https://myaccount.google.com/security
   - Or: Click your profile picture → "Manage your Google Account" → "Security"

2. **Enable 2-Step Verification**
   - Scroll to "How you sign in to Google"
   - Click on "2-Step Verification"
   - Click "GET STARTED"
   - Follow the prompts to set up with your phone number
   - Complete the setup process

✅ **Checkpoint:** You should now have 2-Step Verification enabled.

---

## 🔑 Step 2: Generate Gmail App Password

**Why:** App Passwords allow applications to access your Gmail without using your main password.

### Instructions:

1. **Access App Passwords Page**
   - Visit: https://myaccount.google.com/apppasswords
   - Or: Google Account → Security → 2-Step Verification → App passwords (at the bottom)

2. **Create New App Password**
   - You may need to re-enter your Google password
   - Under "Select app", choose "Mail" or "Other (Custom name)"
   - If you choose "Other", type: `Bike Ta Bai` or `ASP.NET App`
   - Under "Select device", choose "Other (Custom name)"
   - Type: `BiketaBai Server` or `Development Machine`
   - Click "GENERATE"

3. **Copy the Generated Password**
   - Google will show you a 16-character password (with spaces)
   - Example: `abcd efgh ijkl mnop`
   - **IMPORTANT:** Copy this password immediately - you won't be able to see it again!
   - Keep it safe and secure

✅ **Checkpoint:** You now have a 16-character App Password.

---

## ⚙️ Step 3: Configure Your Application

### Update `appsettings.json`

1. Open `/Users/macbookpro/Documents/BiketaBai3.0/appsettings.json`

2. Find the `EmailSettings` section:

```json
"EmailSettings": {
  "SenderName": "Bike Ta Bai",
  "SenderEmail": "your-email@gmail.com",
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": "587",
  "SmtpPassword": "your-app-password-here"
}
```

3. **Replace the values:**
   - `SenderEmail`: Your Gmail address (e.g., `john.doe@gmail.com`)
   - `SmtpPassword`: The 16-character App Password you generated
     - **Remove all spaces** from the password
     - Example: Change `abcd efgh ijkl mnop` to `abcdefghijklmnop`

### Example Configuration:

```json
"EmailSettings": {
  "SenderName": "Bike Ta Bai",
  "SenderEmail": "johndoe@gmail.com",
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": "587",
  "SmtpPassword": "abcdefghijklmnop"
}
```

⚠️ **Security Note:** Never commit your App Password to Git! Use environment variables in production.

---

## 🗄️ Step 4: Update Database Schema

Since we added new fields to the User model, we need to update the database:

### Run Migration Commands:

```bash
cd /Users/macbookpro/Documents/BiketaBai3.0

# Create a new migration
dotnet ef migrations add AddEmailVerificationFields

# Apply the migration
dotnet ef database update
```

This will add these new columns to the `users` table:
- `email_verification_token` (VARCHAR(100))
- `email_verification_token_expires` (DATETIME)

---

## 🧪 Step 5: Test the Email Verification

### 1. Install Required Package (if not done)

```bash
cd /Users/macbookpro/Documents/BiketaBai3.0
dotnet restore
```

### 2. Run the Application

```bash
dotnet run
```

### 3. Test Registration

1. Open browser: http://localhost:5000/Account/Register
2. Fill in the registration form:
   - Use a **real email** you have access to
   - Complete all fields
   - Select Renter and/or Owner
3. Click "Create Account"

### 4. Check Your Email

1. Check your inbox (and spam folder!)
2. Look for email from "Bike Ta Bai"
3. Subject: "Verify Your Email - Bike Ta Bai"
4. Click the "Verify Email Address" button

### 5. Verify Your Account

- You'll be redirected to the verification page
- Should see "Email Verified!" message
- Click "Login to Your Account"

### 6. Login

- Enter your email and password
- You should be able to login successfully!

---

## 🐛 Troubleshooting

### Issue 1: "Unable to connect to SMTP server"

**Solutions:**
- Check your internet connection
- Verify `SmtpServer` is `smtp.gmail.com`
- Verify `SmtpPort` is `587`
- Make sure you're not behind a firewall blocking port 587

### Issue 2: "Authentication failed"

**Solutions:**
- Double-check your Gmail address is correct
- Verify App Password is correct (no spaces!)
- Confirm 2-Step Verification is enabled
- Try generating a new App Password
- Make sure you're using the App Password, NOT your regular Gmail password

### Issue 3: "Email not received"

**Solutions:**
- Check your spam/junk folder
- Wait a few minutes (email delivery can be delayed)
- Check application logs for errors: `logs/biketabai-*.txt`
- Verify the recipient email is correct
- Test sending to a different email address

### Issue 4: "Table 'users' doesn't have column 'email_verification_token'"

**Solution:**
```bash
dotnet ef migrations add AddEmailVerificationFields
dotnet ef database update
```

### Issue 5: "Failed to send verification email" on registration

**Check:**
1. Application logs in `logs/` directory
2. Email settings in `appsettings.json`
3. App Password is correctly configured
4. Try the "Test Email" command below

---

## 📬 Testing Email Sending

To test if email is working without registering, you can create a test endpoint:

Create `/Pages/TestEmail.cshtml.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Services;

namespace BiketaBai.Pages;

public class TestEmailModel : PageModel
{
    private readonly EmailService _emailService;

    public TestEmailModel(EmailService emailService)
    {
        _emailService = emailService;
    }

    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            Message = "Please provide email: /TestEmail?email=yourmail@gmail.com";
            return Page();
        }

        try
        {
            await _emailService.SendVerificationEmailAsync(
                email, 
                "Test User", 
                "http://localhost:5000/Account/VerifyEmail?token=test&email=test@test.com"
            );
            Message = $"Test email sent successfully to {email}!";
        }
        catch (Exception ex)
        {
            Message = $"Error: {ex.Message}";
        }

        return Page();
    }
}
```

Create `/Pages/TestEmail.cshtml`:

```html
@page
@model TestEmailModel
<div class="container">
    <h1>Test Email</h1>
    <p>@Model.Message</p>
</div>
```

Then visit: `http://localhost:5000/TestEmail?email=your-email@gmail.com`

---

## 🔒 Security Best Practices

### For Development:
1. ✅ Use App Passwords (never your main Gmail password)
2. ✅ Add `appsettings.json` to `.gitignore`
3. ✅ Don't commit sensitive credentials

### For Production:
1. ✅ Use environment variables for credentials
2. ✅ Consider using services like SendGrid, AWS SES, or Mailgun
3. ✅ Enable HTTPS only
4. ✅ Rate limit email sending to prevent abuse
5. ✅ Implement email sending queue for reliability

### Update Program.cs for Production:

```csharp
// Read from environment variables in production
if (!builder.Environment.IsDevelopment())
{
    var emailSettings = builder.Configuration.GetSection("EmailSettings");
    emailSettings["SenderEmail"] = Environment.GetEnvironmentVariable("EMAIL_SENDER") ?? emailSettings["SenderEmail"];
    emailSettings["SmtpPassword"] = Environment.GetEnvironmentVariable("EMAIL_PASSWORD") ?? emailSettings["SmtpPassword"];
}
```

---

## 📊 Email Verification Flow Diagram

```
User Registers
     ↓
Generate Token (24h expiry)
     ↓
Save to Database (unverified)
     ↓
Send Verification Email
     ↓
User Clicks Link in Email
     ↓
Verify Token & Email Match
     ↓
Check Token Not Expired
     ↓
Mark Email as Verified
     ↓
Award Points
     ↓
Send Welcome Email
     ↓
User Can Login
```

---

## ✅ Checklist

Before going live, ensure:

- [ ] 2-Step Verification is enabled on Gmail
- [ ] App Password is generated and saved securely
- [ ] `appsettings.json` is configured with correct email/password
- [ ] Database migration has been run
- [ ] Test email sending works
- [ ] Registration sends verification email
- [ ] Verification link works correctly
- [ ] Login blocks unverified users
- [ ] Welcome email is sent after verification
- [ ] Credentials are not in source control

---

## 🎉 Success!

You now have a fully functional email verification system!

### What Users Experience:

1. **Register** → Receive verification email
2. **Click Link** → Email verified + Welcome email
3. **Login** → Access full features

### What You Get:

✅ Verified email addresses
✅ Reduced spam/fake accounts
✅ Better user engagement
✅ Professional onboarding experience

---

## 📚 Additional Resources

- [Google App Passwords Documentation](https://support.google.com/accounts/answer/185833)
- [MailKit Documentation](https://github.com/jstedfast/MailKit)
- [ASP.NET Core Email Documentation](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/email)

---

## 💡 Next Steps

Consider implementing:
- Password reset via email
- Email notification preferences
- Email templates system
- Email sending queue (Hangfire/Background Service)
- Alternative email providers (SendGrid, AWS SES)
- Email analytics/tracking

---

Need help? Check the logs in `/logs/biketabai-*.txt` for detailed error messages!

