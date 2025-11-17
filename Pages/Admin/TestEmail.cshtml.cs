using BiketaBai.Helpers;
using BiketaBai.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Serilog;

namespace BiketaBai.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class TestEmailModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public TestEmailModel(IConfiguration configuration, EmailService emailService)
        {
            _configuration = configuration;
            _emailService = emailService;
        }

        [BindProperty]
        public string TestEmailAddress { get; set; } = string.Empty;

        public TestResult? Result { get; set; }
        public EmailConfigInfo ConfigInfo { get; set; } = new();

        public class EmailConfigInfo
        {
            public string SenderName { get; set; } = string.Empty;
            public string SenderEmail { get; set; } = string.Empty;
            public string SmtpServer { get; set; } = string.Empty;
            public string SmtpPort { get; set; } = string.Empty;
            public string SmtpPasswordMasked { get; set; } = string.Empty;
            public string SmtpAuthEmail { get; set; } = string.Empty;
            public bool IsConfigured { get; set; }
            public string ConfigSource { get; set; } = string.Empty;
        }

        public class TestResult
        {
            public bool Success { get; set; }
            public List<TestStep> Steps { get; set; } = new();
            public string? ErrorMessage { get; set; }
            public string? FullException { get; set; }
        }

        public class TestStep
        {
            public string Name { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string? Details { get; set; }
        }

        public void OnGet()
        {
            LoadConfigInfo();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!AuthHelper.IsAdmin(User))
                return RedirectToPage("/Account/AccessDenied");

            LoadConfigInfo();

            if (string.IsNullOrWhiteSpace(TestEmailAddress))
            {
                Result = new TestResult
                {
                    Success = false,
                    ErrorMessage = "Please enter a test email address"
                };
                return Page();
            }

            Result = await RunSmtpTestAsync(TestEmailAddress);
            return Page();
        }

        private void LoadConfigInfo()
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            
            // Get values directly from IConfiguration (this shows what the app actually uses)
            ConfigInfo.SenderName = emailSettings["SenderName"] ?? "Not set";
            ConfigInfo.SenderEmail = emailSettings["SenderEmail"] ?? "Not set";
            ConfigInfo.SmtpServer = emailSettings["SmtpServer"] ?? "Not set";
            ConfigInfo.SmtpPort = emailSettings["SmtpPort"] ?? "587";
            ConfigInfo.SmtpAuthEmail = emailSettings["SmtpAuthEmail"] ?? "";
            
            // Also check environment variables directly to show what's overriding
            var envSenderEmail = Environment.GetEnvironmentVariable("EmailSettings__SenderEmail") 
                                ?? Environment.GetEnvironmentVariable("EmailSettings:SenderEmail");
            var envSmtpPassword = Environment.GetEnvironmentVariable("EmailSettings__SmtpPassword")
                                ?? Environment.GetEnvironmentVariable("EmailSettings:SmtpPassword");
            
            // Get password - check environment variables first (they override)
            var password = envSmtpPassword?.Trim() 
                         ?? emailSettings["SmtpPassword"]?.Trim() 
                         ?? "";
            
            // If environment variables exist, they override config files
            if (!string.IsNullOrEmpty(envSenderEmail))
            {
                ConfigInfo.SenderEmail = envSenderEmail; // Show the actual value being used
            }
            if (!string.IsNullOrEmpty(password))
            {
                if (password.Length > 8)
                {
                    ConfigInfo.SmtpPasswordMasked = password.Substring(0, 4) + "***" + password.Substring(password.Length - 4);
                }
                else
                {
                    ConfigInfo.SmtpPasswordMasked = "***";
                }
            }
            else
            {
                ConfigInfo.SmtpPasswordMasked = "Not set";
            }

            ConfigInfo.IsConfigured = !string.IsNullOrEmpty(ConfigInfo.SenderEmail) &&
                                     ConfigInfo.SenderEmail != "your-email@example.com" &&
                                     !string.IsNullOrEmpty(password) &&
                                     password != "your-app-password-here";

            // Determine config source - check environment variables first (they override appsettings)
            // Check both ASP.NET Core format (__) and Railway format (:)
            var envSmtpServer = Environment.GetEnvironmentVariable("EmailSettings__SmtpServer")
                              ?? Environment.GetEnvironmentVariable("EmailSettings:SmtpServer");
            
            // Also check Railway-style environment variables
            var railwaySenderEmail = Environment.GetEnvironmentVariable("EmailSettings:SenderEmail");
            var railwaySmtpServer = Environment.GetEnvironmentVariable("EmailSettings:SmtpServer");
            var railwaySmtpPassword = Environment.GetEnvironmentVariable("EmailSettings:SmtpPassword");
            
            // Check all possible env var formats
            var hasEnvVars = !string.IsNullOrEmpty(envSenderEmail) || 
                           !string.IsNullOrEmpty(envSmtpServer) || 
                           !string.IsNullOrEmpty(envSmtpPassword) ||
                           !string.IsNullOrEmpty(railwaySenderEmail) ||
                           !string.IsNullOrEmpty(railwaySmtpServer) ||
                           !string.IsNullOrEmpty(railwaySmtpPassword);
            
            // Check environment
            var environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
            var isProduction = environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
            
            if (hasEnvVars)
            {
                var envVars = new List<string>();
                if (!string.IsNullOrEmpty(envSenderEmail)) envVars.Add($"EmailSettings__SenderEmail={envSenderEmail}");
                if (!string.IsNullOrEmpty(railwaySenderEmail)) envVars.Add($"EmailSettings:SenderEmail={railwaySenderEmail}");
                if (!string.IsNullOrEmpty(envSmtpPassword)) envVars.Add($"EmailSettings__SmtpPassword=***");
                if (!string.IsNullOrEmpty(railwaySmtpPassword)) envVars.Add($"EmailSettings:SmtpPassword=***");
                
                if (isProduction)
                {
                    // In production, environment variables are expected and correct
                    ConfigInfo.ConfigSource = $"✅ Environment Variables (Production)\nFound: {string.Join(", ", envVars)}\n\nThis is normal for Railway deployment. Environment variables securely override config files.";
                }
                else
                {
                    // In development, warn if env vars are set (might be accidental)
                    ConfigInfo.ConfigSource = $"⚠️ Environment Variables OVERRIDE config files!\nFound: {string.Join(", ", envVars)}\n\nIn Development, this might override your local appsettings.Development.json";
                }
            }
            else
            {
                // Check which appsettings file is being used
                var isDevelopment = environment.Equals("Development", StringComparison.OrdinalIgnoreCase);
                ConfigInfo.ConfigSource = isDevelopment ? "appsettings.Development.json" : "appsettings.json";
            }
        }

        private async Task<TestResult> RunSmtpTestAsync(string testEmail)
        {
            var result = new TestResult { Steps = new List<TestStep>() };
            var emailSettings = _configuration.GetSection("EmailSettings");

            try
            {
                // Step 1: Load Configuration
                var step1 = new TestStep { Name = "1. Load Configuration" };
                var senderName = emailSettings["SenderName"]?.Trim();
                var senderEmail = emailSettings["SenderEmail"]?.Trim();
                var smtpServer = emailSettings["SmtpServer"]?.Trim();
                var smtpPort = emailSettings["SmtpPort"]?.Trim() ?? "587";
                var smtpPassword = emailSettings["SmtpPassword"]?.Trim();
                var smtpAuthEmail = emailSettings["SmtpAuthEmail"]?.Trim();

                if (string.IsNullOrEmpty(senderEmail) || senderEmail == "your-email@example.com")
                {
                    step1.Success = false;
                    step1.Message = "❌ SenderEmail is not configured";
                    step1.Details = $"Current value: '{senderEmail}'";
                    result.Steps.Add(step1);
                    result.ErrorMessage = "Email configuration is missing or invalid";
                    return result;
                }

                if (string.IsNullOrEmpty(smtpPassword) || smtpPassword == "your-app-password-here")
                {
                    step1.Success = false;
                    step1.Message = "❌ SmtpPassword is not configured";
                    step1.Details = "Password is missing or placeholder";
                    result.Steps.Add(step1);
                    result.ErrorMessage = "SMTP password is not configured";
                    return result;
                }

                // Auto-detect server if not set
                if (string.IsNullOrEmpty(smtpServer))
                {
                    if (senderEmail.Contains("@icloud.com") || senderEmail.Contains("@me.com") || senderEmail.Contains("@mac.com"))
                    {
                        smtpServer = "smtp.mail.me.com";
                    }
                    else
                    {
                        smtpServer = "smtp.gmail.com";
                    }
                }

                step1.Success = true;
                step1.Message = "✅ Configuration loaded successfully";
                step1.Details = $"Server: {smtpServer}:{smtpPort}, From: {senderEmail}, Password length: {smtpPassword?.Length ?? 0}";
                result.Steps.Add(step1);

                // Step 2: Create Test Message
                var step2 = new TestStep { Name = "2. Create Test Message" };
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(senderName ?? "Bike Ta Bai", senderEmail));
                message.To.Add(new MailboxAddress("", testEmail));
                message.Subject = $"SMTP Test - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                message.Body = new TextPart("plain")
                {
                    Text = $"This is a test email from Bike Ta Bai SMTP service.\n\nSent at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\nIf you receive this, your SMTP configuration is working correctly!"
                };

                step2.Success = true;
                step2.Message = "✅ Test message created";
                step2.Details = $"To: {testEmail}, Subject: {message.Subject}";
                result.Steps.Add(step2);

                // Step 3: Connect to SMTP Server
                var step3 = new TestStep { Name = "3. Connect to SMTP Server" };
                using var client = new SmtpClient();
                
                // Set timeout (30 seconds)
                client.Timeout = 30000;

                try
                {
                    var port = int.Parse(smtpPort);
                    var secureOption = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                    
                    Log.Information("SMTP Test: Connecting to {Server}:{Port} with {SecureOption}", smtpServer, smtpPort, secureOption);
                    
                    // Try primary connection
                    await client.ConnectAsync(smtpServer, port, secureOption);
                    
                    step3.Success = true;
                    step3.Message = "✅ Connected to SMTP server";
                    step3.Details = $"Server: {smtpServer}:{smtpPort}, Capabilities: {client.Capabilities}";
                    result.Steps.Add(step3);
                }
                catch (Exception ex)
                {
                    step3.Success = false;
                    step3.Message = "❌ Failed to connect to SMTP server";
                    
                    var errorDetails = $"Error: {ex.Message}";
                    
                    // Add Railway-specific troubleshooting
                    if (ex.Message.Contains("timeout") || ex.Message.Contains("timed out"))
                    {
                        errorDetails += $"\n\n⚠️ Connection Timeout - Possible Causes:\n";
                        errorDetails += $"• Railway free tier may block outbound SMTP connections\n";
                        errorDetails += $"• Port {smtpPort} may be blocked by firewall\n";
                        errorDetails += $"• iCloud SMTP may be blocking Railway IP addresses\n";
                        errorDetails += $"\n💡 Solutions:\n";
                        errorDetails += $"• Try port 465 (SSL) instead of 587 (STARTTLS)\n";
                        errorDetails += $"• Use Railway's SMTP service or third-party email service (SendGrid, Mailgun)\n";
                        errorDetails += $"• Upgrade Railway plan (may have fewer restrictions)";
                    }
                    
                    step3.Details = errorDetails;
                    result.Steps.Add(step3);
                    result.ErrorMessage = $"Connection failed: {ex.Message}";
                    result.FullException = ex.ToString();
                    return result;
                }

                // Step 4: Authenticate
                var step4 = new TestStep { Name = "4. Authenticate" };
                var authEmail = !string.IsNullOrEmpty(smtpAuthEmail) ? smtpAuthEmail : senderEmail;
                
                try
                {
                    Log.Information("SMTP Test: Authenticating with {AuthEmail}", authEmail);
                    await client.AuthenticateAsync(authEmail, smtpPassword);
                    
                    step4.Success = true;
                    step4.Message = "✅ Authentication successful";
                    step4.Details = $"Authenticated as: {authEmail}";
                    result.Steps.Add(step4);
                }
                catch (Exception ex)
                {
                    step4.Success = false;
                    step4.Message = "❌ Authentication failed";
                    step4.Details = $"Error: {ex.Message}\n\nCommon causes:\n- Wrong password (check for spaces/typos)\n- App password not generated\n- 2FA not enabled (for Gmail)\n- Account locked/restricted";
                    result.Steps.Add(step4);
                    result.ErrorMessage = $"Authentication failed: {ex.Message}";
                    result.FullException = ex.ToString();
                    
                    try
                    {
                        await client.DisconnectAsync(true);
                    }
                    catch { }
                    
                    return result;
                }

                // Step 5: Send Email
                var step5 = new TestStep { Name = "5. Send Email" };
                try
                {
                    Log.Information("SMTP Test: Sending email to {TestEmail}", testEmail);
                    await client.SendAsync(message);
                    
                    step5.Success = true;
                    step5.Message = "✅ Email sent successfully";
                    step5.Details = $"Email sent to: {testEmail}";
                    result.Steps.Add(step5);
                }
                catch (Exception ex)
                {
                    step5.Success = false;
                    step5.Message = "❌ Failed to send email";
                    step5.Details = $"Error: {ex.Message}";
                    result.Steps.Add(step5);
                    result.ErrorMessage = $"Send failed: {ex.Message}";
                    result.FullException = ex.ToString();
                    
                    try
                    {
                        await client.DisconnectAsync(true);
                    }
                    catch { }
                    
                    return result;
                }

                // Step 6: Disconnect
                var step6 = new TestStep { Name = "6. Disconnect" };
                try
                {
                    await client.DisconnectAsync(true);
                    step6.Success = true;
                    step6.Message = "✅ Disconnected from SMTP server";
                    result.Steps.Add(step6);
                }
                catch (Exception ex)
                {
                    step6.Success = false;
                    step6.Message = "⚠️ Disconnect warning";
                    step6.Details = $"Error: {ex.Message} (non-critical)";
                    result.Steps.Add(step6);
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SMTP Test failed with exception");
                result.ErrorMessage = ex.Message;
                result.FullException = ex.ToString();
            }

            return result;
        }
    }
}

