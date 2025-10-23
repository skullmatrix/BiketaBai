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
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .content {{
            background-color: white;
            padding: 30px;
            border-radius: 10px;
        }}
        .header {{
            text-align: center;
            color: #2d7f3e;
            margin-bottom: 30px;
        }}
        .button {{
            display: inline-block;
            padding: 15px 30px;
            background-color: #2d7f3e;
            color: white;
            text-decoration: none;
            border-radius: 5px;
            margin: 20px 0;
        }}
        .footer {{
            text-align: center;
            margin-top: 20px;
            font-size: 12px;
            color: #666;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='content'>
            <div class='header'>
                <h1>🚴 Bike Ta Bai</h1>
                <h2>Email Verification</h2>
            </div>
            
            <p>Hi <strong>{fullName}</strong>,</p>
            
            <p>Thank you for registering with Bike Ta Bai! We're excited to have you join our eco-friendly bike rental community.</p>
            
            <p>To complete your registration and activate your account, please verify your email address by clicking the button below:</p>
            
            <div style='text-align: center;'>
                <a href='{verificationLink}' class='button'>Verify Email Address</a>
            </div>
            
            <p>Or copy and paste this link into your browser:</p>
            <p style='background-color: #f0f0f0; padding: 10px; word-break: break-all;'>{verificationLink}</p>
            
            <p><strong>This verification link will expire in 24 hours.</strong></p>
            
            <p>If you didn't create an account with Bike Ta Bai, please ignore this email.</p>
            
            <div class='footer'>
                <p>Best regards,<br>The Bike Ta Bai Team</p>
                <p>🌱 Ride Green, Live Clean 🌱</p>
            </div>
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
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .content {{
            background-color: white;
            padding: 30px;
            border-radius: 10px;
        }}
        .header {{
            text-align: center;
            color: #2d7f3e;
            margin-bottom: 30px;
        }}
        .feature {{
            background-color: #f1f8f4;
            padding: 15px;
            margin: 10px 0;
            border-radius: 5px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='content'>
            <div class='header'>
                <h1>🚴 Welcome to Bike Ta Bai!</h1>
            </div>
            
            <p>Hi <strong>{fullName}</strong>,</p>
            
            <p>Your email has been verified successfully! You're all set to start your eco-friendly journey with us.</p>
            
            <h3>What's Next?</h3>
            
            <div class='feature'>
                <strong>🔍 Browse Bikes</strong><br>
                Explore our wide selection of bikes available for rent in your area.
            </div>
            
            <div class='feature'>
                <strong>💰 Load Your Wallet</strong><br>
                Add funds to your wallet for seamless bookings and transactions.
            </div>
            
            <div class='feature'>
                <strong>⭐ Earn Points</strong><br>
                Every rental earns you loyalty points that you can redeem for credits!
            </div>
            
            <div class='feature'>
                <strong>🌱 Track Your Impact</strong><br>
                See how much CO₂ you're saving with each eco-friendly ride.
            </div>
            
            <p style='text-align: center; margin: 30px 0;'>
                <a href='http://localhost:5000' style='display: inline-block; padding: 15px 30px; background-color: #2d7f3e; color: white; text-decoration: none; border-radius: 5px;'>Start Browsing Bikes</a>
            </p>
            
            <p style='text-align: center; color: #666; font-size: 12px;'>
                Thank you for choosing eco-friendly transportation!<br>
                🌱 Ride Green, Live Clean 🌱
            </p>
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(toEmail, subject, htmlBody);
    }
}

