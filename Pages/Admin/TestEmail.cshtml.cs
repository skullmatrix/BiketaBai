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
using SendGrid;
using SendGrid.Helpers.Mail;

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

                // Check if using SendGrid - use REST API instead of SMTP
                var isSendGrid = smtpServer?.Contains("sendgrid", StringComparison.OrdinalIgnoreCase) == true;
                
                if (isSendGrid)
                {
                    // Use SendGrid REST API (bypasses Railway SMTP restrictions)
                    var step3 = new TestStep { Name = "3. SendGrid REST API" };
                    
                    try
                    {
                        var apiKey = smtpPassword?.Trim() ?? string.Empty;
                        if (string.IsNullOrEmpty(apiKey) || !apiKey.StartsWith("SG.", StringComparison.OrdinalIgnoreCase))
                        {
                            step3.Success = false;
                            step3.Message = "❌ Invalid SendGrid API Key";
                            step3.Details = "SendGrid API key must start with 'SG.'. Please check your EmailSettings__SmtpPassword in Railway.";
                            result.Steps.Add(step3);
                            result.ErrorMessage = "Invalid SendGrid API key";
                            return result;
                        }

                        var client = new SendGridClient(apiKey);
                        var from = new EmailAddress(senderEmail, senderName ?? "Bike Ta Bai");
                        var to = new EmailAddress(testEmail);
                        
                        var msg = MailHelper.CreateSingleEmail(from, to, message.Subject, null, 
                            message.Body is TextPart textPart ? textPart.Text : "Test email from Bike Ta Bai");
                        
                        Log.Information("SMTP Test: Sending via SendGrid REST API to {TestEmail}", testEmail);
                        var response = await client.SendEmailAsync(msg);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            step3.Success = true;
                            step3.Message = "✅ Email sent via SendGrid REST API";
                            step3.Details = $"Status: {response.StatusCode}, To: {testEmail}\n\n✅ SendGrid REST API works with Railway!";
                            result.Steps.Add(step3);
                            
                            result.Success = true;
                            return result;
                        }
                        else
                        {
                            var responseBody = await response.Body.ReadAsStringAsync();
                            step3.Success = false;
                            step3.Message = "❌ SendGrid API error";
                            step3.Details = $"Status: {response.StatusCode}\nResponse: {responseBody}";
                            result.Steps.Add(step3);
                            result.ErrorMessage = $"SendGrid API error: {response.StatusCode}";
                            result.FullException = responseBody;
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        step3.Success = false;
                        step3.Message = "❌ SendGrid REST API failed";
                        step3.Details = $"Error: {ex.Message}";
                        result.Steps.Add(step3);
                        result.ErrorMessage = $"SendGrid API failed: {ex.Message}";
                        result.FullException = ex.ToString();
                        return result;
                    }
                }
                else
                {
                    // Use traditional SMTP for other providers
                    // Step 3: Connect to SMTP Server
                    var step3 = new TestStep { Name = "3. Connect to SMTP Server" };
                    using var client = new SmtpClient();
                    
                    // Set timeout (30 minutes)
                    client.Timeout = 1800000; // 30 minutes = 30 * 60 * 1000 milliseconds

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
                        
                        // Check for timeout exception (Railway blocks SMTP)
                        var isTimeout = ex is System.TimeoutException || 
                                       ex.GetType().Name.Contains("Timeout") ||
                                       ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) || 
                                       ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
                        
                        if (isTimeout)
                        {
                            step3.Message = "🚫 RAILWAY BLOCKS SMTP - Use SendGrid REST API";
                            errorDetails = $"\n\n🚫 RAILWAY FREE TIER LIMITATION DETECTED:\n";
                            errorDetails += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
                            errorDetails += $"Railway's free tier BLOCKS all outbound SMTP connections.\n";
                            errorDetails += $"Ports 587, 465, and 25 are blocked by Railway's firewall.\n";
                            errorDetails += $"This is a network restriction - NOT a configuration issue.\n";
                            errorDetails += $"\n✅ SOLUTION: Use SendGrid REST API (already implemented!)\n";
                            errorDetails += $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
                            errorDetails += $"\n📝 Update Railway Variables:\n";
                            errorDetails += $"\n   EmailSettings__SmtpServer=smtp.sendgrid.net\n";
                            errorDetails += $"   EmailSettings__SmtpPassword=<your-sendgrid-api-key>\n";
                            errorDetails += $"   EmailSettings__SenderEmail=admin@biketabai.net\n";
                            errorDetails += $"\n💡 The code now automatically uses SendGrid REST API when SendGrid is detected!\n";
                            errorDetails += $"   This bypasses Railway's SMTP restrictions.";
                        }
                        
                        step3.Details = errorDetails;
                        result.Steps.Add(step3);
                        result.ErrorMessage = isTimeout 
                            ? "Railway blocks SMTP connections. Use SendGrid REST API instead."
                            : $"Connection failed: {ex.Message}";
                        result.FullException = ex.ToString();
                        return result;
                    }
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

