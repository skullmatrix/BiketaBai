using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;

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
        
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            emailSettings["SenderName"], 
            emailSettings["SenderEmail"]
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
            await client.ConnectAsync(
                emailSettings["SmtpServer"], 
                int.Parse(emailSettings["SmtpPort"]!), 
                SecureSocketOptions.StartTls
            );

            await client.AuthenticateAsync(
                emailSettings["SenderEmail"], 
                emailSettings["SmtpPassword"]
            );

            await client.SendAsync(message);
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"Email sending failed: {ex.Message}");
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true);
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

