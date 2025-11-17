using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Serilog;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace BiketaBai.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var emailSettings = _configuration.GetSection("EmailSettings");
        
        // Get email settings and trim whitespace (important for passwords!)
        var senderName = emailSettings["SenderName"]?.Trim();
        var senderEmail = emailSettings["SenderEmail"]?.Trim();
        var smtpServer = emailSettings["SmtpServer"]?.Trim();
        var smtpPort = emailSettings["SmtpPort"]?.Trim();
        var smtpPassword = emailSettings["SmtpPassword"]?.Trim(); // CRITICAL: Trim password to remove any whitespace
        // Optional: For iCloud custom domain, you might need separate auth email
        var smtpAuthEmail = emailSettings["SmtpAuthEmail"]?.Trim(); // Optional - defaults to SenderEmail

        // Validate configuration
        if (string.IsNullOrEmpty(senderEmail) || senderEmail == "your-email@gmail.com" || senderEmail.Contains("REPLACE"))
        {
            var errorMsg = $"Email sender address is not configured properly. Current value: '{senderEmail}'. Please set EmailSettings:SenderEmail in appsettings.json or appsettings.Development.json";
            Log.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        if (string.IsNullOrEmpty(smtpPassword) || smtpPassword == "your-app-password-here" || smtpPassword.Contains("REPLACE"))
        {
            var errorMsg = $"Email SMTP password is not configured properly. Current value: '{smtpPassword?.Substring(0, Math.Min(10, smtpPassword?.Length ?? 0))}...'. Please set EmailSettings:SmtpPassword in appsettings.json or appsettings.Development.json";
            Log.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        // Validate SMTP server - must be a hostname, not an email address
        if (!string.IsNullOrEmpty(smtpServer) && smtpServer.Contains("@"))
        {
            Log.Warning("SMTP Server appears to be an email address ({SmtpServer}). This is incorrect. Auto-detecting from sender email.", smtpServer);
            smtpServer = null; // Reset to trigger auto-detection
        }

        if (string.IsNullOrEmpty(smtpServer))
        {
            // Auto-detect based on email domain
            var emailDomain = senderEmail?.Split('@').LastOrDefault()?.ToLower();
            
            if (senderEmail?.Contains("@icloud.com") == true || 
                senderEmail?.Contains("@me.com") == true || 
                senderEmail?.Contains("@mac.com") == true)
            {
                smtpServer = "smtp.mail.me.com"; // iCloud SMTP
            }
            else if (senderEmail?.Contains("@gmail.com") == true)
            {
                smtpServer = "smtp.gmail.com"; // Gmail SMTP
            }
            else if (emailDomain != null)
            {
                // For custom domains, try common patterns
                // If using iCloud Custom Domain, use iCloud SMTP
                // If using Google Workspace, use Gmail SMTP
                // Otherwise, try smtp.{domain}
                
                // Check if it might be iCloud Custom Domain (common setup)
                // You can override this by explicitly setting SmtpServer
                smtpServer = $"smtp.{emailDomain}"; // Try smtp.yourdomain.com
                
                Log.Information("Auto-detected SMTP server as {SmtpServer} for domain {Domain}. " +
                              "If this is incorrect, explicitly set EmailSettings:SmtpServer. " +
                              "For iCloud Custom Domain, use: smtp.mail.me.com. " +
                              "For Google Workspace, use: smtp.gmail.com", 
                              smtpServer, emailDomain);
            }
            else
            {
                smtpServer = "smtp.gmail.com"; // Default fallback
            }
        }

        if (string.IsNullOrEmpty(smtpPort))
        {
            smtpPort = "587"; // Default port
        }

        // Check if using SendGrid - use REST API instead of SMTP (works with Railway)
        var isSendGrid = smtpServer?.Contains("sendgrid", StringComparison.OrdinalIgnoreCase) == true;
        
        if (isSendGrid)
        {
            // Use SendGrid REST API (works with Railway - uses HTTPS port 443)
            Log.Information("Using SendGrid REST API (bypasses Railway SMTP restrictions)");
            
            try
            {
                var apiKey = smtpPassword?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(apiKey) || !apiKey.StartsWith("SG.", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("SendGrid API key is invalid. It should start with 'SG.'");
                }

                var client = new SendGridClient(apiKey);
                var from = new EmailAddress(senderEmail, senderName ?? "Bike Ta Bai");
                var to = new EmailAddress(toEmail);
                
                var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlBody);
                
                Log.Information("Sending email via SendGrid REST API to {ToEmail}", toEmail);
                var response = await client.SendEmailAsync(msg);
                
                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Email sent successfully via SendGrid to {ToEmail}", toEmail);
                }
                else
                {
                    var responseBody = await response.Body.ReadAsStringAsync();
                    Log.Error("SendGrid API error. Status: {StatusCode}, Body: {ResponseBody}", response.StatusCode, responseBody);
                    throw new InvalidOperationException($"SendGrid API error: {response.StatusCode} - {responseBody}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SendGrid REST API failed. To: {ToEmail}, From: {SenderEmail}. Error: {ErrorMessage}", 
                    toEmail, senderEmail, ex.Message);
                throw;
            }
        }
        else
        {
            // Use traditional SMTP for other providers
            Log.Information("Attempting to send email to {ToEmail} from {SenderEmail} via {SmtpServer}:{SmtpPort}", 
                toEmail, senderEmail, smtpServer, smtpPort);
            Log.Information("Email password length: {PasswordLength}, Starts with: {PasswordStart}, Ends with: {PasswordEnd}", 
                smtpPassword?.Length ?? 0, 
                smtpPassword?.Substring(0, Math.Min(4, smtpPassword?.Length ?? 0)) ?? "null",
                smtpPassword?.Substring(Math.Max(0, (smtpPassword?.Length ?? 0) - 4)) ?? "null");
            Log.Information("Auth email: {AuthEmail}, Sender email: {SenderEmail}", 
                !string.IsNullOrEmpty(smtpAuthEmail) ? smtpAuthEmail : senderEmail, senderEmail);
            
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                senderName ?? "Bike Ta Bai", 
                senderEmail
            ));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                // Set timeout for SMTP operations (30 minutes)
                client.Timeout = 1800000; // 30 minutes = 30 * 60 * 1000 milliseconds
                
                Log.Information("Connecting to SMTP server {SmtpServer}:{SmtpPort} with STARTTLS", smtpServer, smtpPort);
                
                // Try connecting with timeout
                var port = int.Parse(smtpPort);
                var secureOption = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                
                // Connect with STARTTLS (port 587) or SSL (port 465)
                await client.ConnectAsync(
                    smtpServer, 
                    port, 
                    secureOption
                );

                Log.Information("Connected to SMTP server successfully. Server capabilities: {Capabilities}", client.Capabilities);

                // For iCloud custom domain, use auth email if provided, otherwise use sender email
                var authEmail = !string.IsNullOrEmpty(smtpAuthEmail) ? smtpAuthEmail : senderEmail;
                
                // Ensure password is trimmed (critical for Apple app passwords with dashes)
                var trimmedPassword = smtpPassword?.Trim() ?? string.Empty;
                
                Log.Information("Authenticating with email: {AuthEmail} (sending from: {SenderEmail}). Password length: {PasswordLength}", 
                    authEmail, senderEmail, trimmedPassword.Length);
                
                // Authenticate with credentials (ensure password is trimmed)
                await client.AuthenticateAsync(
                    authEmail, 
                    trimmedPassword
                );
                
                Log.Information("Authentication successful!");

                Log.Information("Sending email to {ToEmail}", toEmail);
                await client.SendAsync(message);
                Log.Information("Email sent successfully to {ToEmail}", toEmail);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Email sending failed. To: {ToEmail}, From: {SenderEmail}, SMTP: {SmtpServer}:{SmtpPort}. Error: {ErrorMessage}", 
                    toEmail, senderEmail, smtpServer, smtpPort, ex.Message);
                throw;
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }
    }

    public async Task SendVerificationEmailAsync(string toEmail, string fullName, string verificationLink)
    {
        var subject = "Verify Your Email - Bike Ta Bai";
        
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333333;
            background-color: #f4f4f4;
            margin: 0;
            padding: 0;
        }}
        .container {{
            max-width: 600px;
            margin: 20px auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            background: linear-gradient(135deg, #87A96B 0%, #6B8E5F 100%);
            color: #ffffff;
            padding: 30px 20px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .content h2 {{
            color: #87A96B;
            margin-top: 0;
        }}
        .button {{
            display: inline-block;
            padding: 15px 30px;
            background-color: #87A96B;
            color: #ffffff;
            text-decoration: none;
            border-radius: 5px;
            font-weight: bold;
            margin: 20px 0;
        }}
        .button:hover {{
            background-color: #6B8E5F;
        }}
        .footer {{
            background-color: #f8f8f8;
            padding: 20px;
            text-align: center;
            font-size: 12px;
            color: #666666;
        }}
        .link {{
            color: #87A96B;
            word-break: break-all;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🚴 Bike Ta Bai</h1>
        </div>
        <div class='content'>
            <h2>Email Verification</h2>
            <p>Hello {fullName},</p>
            <p>Thank you for signing up with Bike Ta Bai! We're excited to have you join our eco-friendly bike sharing community.</p>
            <p>Please verify your email address by clicking the button below:</p>
            <center>
                <a href='{verificationLink}' class='button'>Verify Email Address</a>
            </center>
            <p>Or copy and paste this link into your browser:</p>
            <p class='link'>{verificationLink}</p>
            <p><strong>This link will expire in 24 hours.</strong></p>
            <p>If you didn't create an account with Bike Ta Bai, please ignore this email.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2024 Bike Ta Bai - Eco-Friendly Bike Sharing</p>
            <p>🌿 Ride Green, Live Clean</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, htmlBody);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string fullName)
    {
        var subject = "Welcome to Bike Ta Bai! 🚴";
        
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333333;
            background-color: #f4f4f4;
            margin: 0;
            padding: 0;
        }}
        .container {{
            max-width: 600px;
            margin: 20px auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            background: linear-gradient(135deg, #87A96B 0%, #6B8E5F 100%);
            color: #ffffff;
            padding: 30px 20px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .content h2 {{
            color: #87A96B;
            margin-top: 0;
        }}
        .features {{
            margin: 30px 0;
        }}
        .feature {{
            background-color: #f8f8f8;
            padding: 15px;
            margin: 10px 0;
            border-radius: 5px;
            border-left: 4px solid #87A96B;
        }}
        .feature strong {{
            color: #87A96B;
        }}
        .button {{
            display: inline-block;
            padding: 15px 30px;
            background-color: #87A96B;
            color: #ffffff;
            text-decoration: none;
            border-radius: 5px;
            font-weight: bold;
            margin: 20px 0;
        }}
        .button:hover {{
            background-color: #6B8E5F;
        }}
        .footer {{
            background-color: #f8f8f8;
            padding: 20px;
            text-align: center;
            font-size: 12px;
            color: #666666;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎉 Welcome to Bike Ta Bai!</h1>
        </div>
        <div class='content'>
            <h2>Your Account is Ready!</h2>
            <p>Hello {fullName},</p>
            <p>Congratulations! Your email has been verified and your account is now active. You're all set to start your eco-friendly journey with Bike Ta Bai.</p>
            
            <div class='features'>
                <div class='feature'>
                    <strong>🚴 Browse Bikes</strong><br>
                    Discover available bikes in your area with real-time availability.
                </div>
                <div class='feature'>
                    <strong>💰 Manage Your Wallet</strong><br>
                    Add funds and enjoy seamless booking experiences.
                </div>
                <div class='feature'>
                    <strong>⭐ Earn Loyalty Points</strong><br>
                    Get rewarded with every rental and unlock special benefits.
                </div>
                <div class='feature'>
                    <strong>🌱 Track Your Impact</strong><br>
                    See your CO₂ savings and contribute to a greener planet.
                </div>
            </div>
            
            <center>
                <a href='http://localhost:5000' class='button'>Start Browsing Bikes</a>
            </center>
            
            <p>If you have any questions, feel free to contact our support team.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2024 Bike Ta Bai - Eco-Friendly Bike Sharing</p>
            <p>🌿 Ride Green, Live Clean</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, htmlBody);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetLink)
    {
        var subject = "Reset Your Password - Bike Ta Bai";
        
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333333;
            background-color: #f4f4f4;
            margin: 0;
            padding: 0;
        }}
        .container {{
            max-width: 600px;
            margin: 20px auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            background: linear-gradient(135deg, #87A96B 0%, #6B8E5F 100%);
            color: #ffffff;
            padding: 30px 20px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .content h2 {{
            color: #87A96B;
            margin-top: 0;
        }}
        .button {{
            display: inline-block;
            padding: 15px 30px;
            background-color: #87A96B;
            color: #ffffff;
            text-decoration: none;
            border-radius: 5px;
            font-weight: bold;
            margin: 20px 0;
        }}
        .button:hover {{
            background-color: #6B8E5F;
        }}
        .warning {{
            background-color: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 15px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .footer {{
            background-color: #f8f8f8;
            padding: 20px;
            text-align: center;
            font-size: 12px;
            color: #666666;
        }}
        .link {{
            color: #87A96B;
            word-break: break-all;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🔑 Password Reset Request</h1>
        </div>
        <div class='content'>
            <h2>Reset Your Password</h2>
            <p>Hello {fullName},</p>
            <p>We received a request to reset your password for your Bike Ta Bai account. Click the button below to create a new password:</p>
            <center>
                <a href='{resetLink}' class='button'>Reset Password</a>
            </center>
            <p>Or copy and paste this link into your browser:</p>
            <p class='link'>{resetLink}</p>
            
            <div class='warning'>
                <strong>⚠️ Important Security Information:</strong>
                <ul>
                    <li>This link will expire in <strong>1 hour</strong></li>
                    <li>If you didn't request this password reset, please ignore this email</li>
                    <li>Your password will not change until you access the link above and create a new one</li>
                </ul>
            </div>
        </div>
        <div class='footer'>
            <p>&copy; 2024 Bike Ta Bai - Eco-Friendly Bike Sharing</p>
            <p>🌿 Ride Green, Live Clean</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, htmlBody);
    }

    public async Task SendPasswordChangedNotificationAsync(string toEmail, string fullName)
    {
        var subject = "Password Changed Successfully - Bike Ta Bai";
        
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333333;
            background-color: #f4f4f4;
            margin: 0;
            padding: 0;
        }}
        .container {{
            max-width: 600px;
            margin: 20px auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            background: linear-gradient(135deg, #87A96B 0%, #6B8E5F 100%);
            color: #ffffff;
            padding: 30px 20px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 28px;
        }}
        .content {{
            padding: 40px 30px;
        }}
        .content h2 {{
            color: #87A96B;
            margin-top: 0;
        }}
        .success {{
            background-color: #d4edda;
            border-left: 4px solid #28a745;
            padding: 15px;
            margin: 20px 0;
            border-radius: 5px;
            color: #155724;
        }}
        .warning {{
            background-color: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 15px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .button {{
            display: inline-block;
            padding: 15px 30px;
            background-color: #87A96B;
            color: #ffffff;
            text-decoration: none;
            border-radius: 5px;
            font-weight: bold;
            margin: 20px 0;
        }}
        .button:hover {{
            background-color: #6B8E5F;
        }}
        .footer {{
            background-color: #f8f8f8;
            padding: 20px;
            text-align: center;
            font-size: 12px;
            color: #666666;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>✅ Password Changed Successfully</h1>
        </div>
        <div class='content'>
            <h2>Security Notification</h2>
            <p>Hello {fullName},</p>
            
            <div class='success'>
                <strong>✓ Your password has been changed successfully!</strong>
            </div>
            
            <p>This email confirms that your Bike Ta Bai account password was recently changed. You can now use your new password to log in to your account.</p>
            
            <div class='warning'>
                <strong>⚠️ Didn't Make This Change?</strong><br>
                If you did not change your password, please contact our support team immediately. Your account security may be at risk.
            </div>
            
            <center>
                <a href='http://localhost:5000/Account/Login' class='button'>Login to Your Account</a>
            </center>
            
            <p>Thank you for keeping your account secure!</p>
        </div>
        <div class='footer'>
            <p>&copy; 2024 Bike Ta Bai - Eco-Friendly Bike Sharing</p>
            <p>🌿 Ride Green, Live Clean</p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, htmlBody);
    }
}

