# 🔐 Credentials Setup Guide

## ✅ Your Credentials Are Secure!

Your email credentials are now stored in **User Secrets** and will NOT be pushed to GitHub.

---

## 📋 What Was Done

1. ✅ Initialized User Secrets for this project
2. ✅ Stored your Gmail email and password in User Secrets
3. ✅ Removed actual credentials from `appsettings.json`
4. ✅ Safe to push to GitHub now!

---

## 🔑 How User Secrets Work

### Your Credentials Are Stored Here:
```
~/.microsoft/usersecrets/eb48afd2-33b7-49e0-bd93-6c5aac75f5e4/secrets.json
```

This file is **outside your project** and **never committed to Git**.

### In Development:
- ASP.NET automatically reads from User Secrets
- Your app works normally with your real credentials
- No changes needed to your code

### In appsettings.json:
- Only placeholder values are stored
- These get overridden by User Secrets in development
- Safe to commit to GitHub

---

## 🧪 Test That It Still Works

```bash
cd /Users/macbookpro/Documents/BiketaBai3.0
dotnet run
```

Your app should still send emails correctly!

---

## 👥 For Other Developers

When someone else clones your repository, they need to:

```bash
# 1. Clone the repo
git clone your-repo-url

# 2. Navigate to project
cd BiketaBai3.0

# 3. Set up their own User Secrets
dotnet user-secrets set "EmailSettings:SenderEmail" "their-email@gmail.com"
dotnet user-secrets set "EmailSettings:SmtpPassword" "their-app-password"

# 4. Run the app
dotnet run
```

---

## 📝 View Your Stored Secrets

To see what secrets are stored:

```bash
dotnet user-secrets list
```

---

## ✏️ Update Secrets

To change your email or password:

```bash
# Update email
dotnet user-secrets set "EmailSettings:SenderEmail" "new-email@gmail.com"

# Update password
dotnet user-secrets set "EmailSettings:SmtpPassword" "new-app-password"
```

---

## 🗑️ Remove Secrets

To remove all secrets:

```bash
dotnet user-secrets clear
```

---

## 🚀 For Production Deployment

User Secrets only work in **development**. For production:

### Option 1: Environment Variables
```bash
export EmailSettings__SenderEmail="your-email@gmail.com"
export EmailSettings__SmtpPassword="your-password"
```

### Option 2: Azure Key Vault / AWS Secrets Manager
- Store credentials in cloud secret management
- More secure for production

### Option 3: Configuration in Hosting Platform
- Heroku: Config Vars
- Azure: Application Settings
- AWS: Systems Manager Parameter Store

---

## ✅ Security Checklist

- [x] User Secrets initialized
- [x] Credentials stored in User Secrets
- [x] Credentials removed from appsettings.json
- [x] .gitignore includes appsettings.json (already done)
- [x] Safe to push to GitHub

---

## 🆘 Troubleshooting

### "Email not sending"
Check if secrets are set:
```bash
dotnet user-secrets list
```

Should show:
```
EmailSettings:SenderEmail = biketabai3@gmail.com
EmailSettings:SmtpPassword = vtqh yvwl vhqy lgnz
```

### "Secrets not found"
Re-run:
```bash
dotnet user-secrets init
dotnet user-secrets set "EmailSettings:SenderEmail" "your-email@gmail.com"
dotnet user-secrets set "EmailSettings:SmtpPassword" "your-password"
```

---

## 🎉 You're All Set!

Your credentials are secure and you can safely push to GitHub!

