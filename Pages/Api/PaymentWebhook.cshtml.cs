using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using BiketaBai.Data;
using BiketaBai.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text;
using System.Text.Json;

namespace BiketaBai.Pages.Api;

[IgnoreAntiforgeryToken]
public class PaymentWebhookModel : PageModel
{
    private readonly BiketaBaiDbContext _context;
    private readonly PaymentService _paymentService;
    private readonly IConfiguration _configuration;

    public PaymentWebhookModel(
        BiketaBaiDbContext context, 
        PaymentService paymentService,
        IConfiguration configuration)
    {
        _context = context;
        _paymentService = paymentService;
        _configuration = configuration;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            // Read request body
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            Log.Information("Payment webhook received: {Body}", body);

            // Verify webhook signature (PayMongo sends signature in headers)
            var webhookSecret = _configuration["AppSettings:PayMongoWebhookSecret"];
            var signature = Request.Headers["Paymongo-Signature"].FirstOrDefault();
            
            // Note: In production, verify the webhook signature using HMAC
            // For now, we'll just check if webhook secret is configured
            if (string.IsNullOrEmpty(webhookSecret))
            {
                Log.Warning("PayMongo webhook secret not configured");
            }

            // Parse webhook payload (PayMongo format)
            var webhookData = JsonSerializer.Deserialize<PayMongoWebhookPayload>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhookData?.Data != null)
            {
                var paymentIntentId = webhookData.Data.Attributes?.Data?.Id ?? "";
                var eventType = webhookData.Data.Type ?? "";
                var status = webhookData.Data.Attributes?.Data?.Attributes?.Status ?? "";

                Log.Information("PayMongo webhook event: {EventType}, PaymentIntent: {PaymentIntentId}, Status: {Status}", 
                    eventType, paymentIntentId, status);

                // Handle payment succeeded events
                // PayMongo events: payment_intent.succeeded, payment_intent.payment_failed, etc.
                if (eventType == "payment_intent.succeeded" || status == "succeeded")
                {
                    if (!string.IsNullOrEmpty(paymentIntentId))
                    {
                        var result = await _paymentService.ConfirmGatewayPaymentAsync(paymentIntentId);
                        
                        if (result.success)
                        {
                            Log.Information("Payment confirmed via PayMongo webhook: {PaymentIntentId}", paymentIntentId);
                            return new OkResult();
                        }
                        else
                        {
                            Log.Warning("Failed to confirm payment via webhook: {PaymentIntentId}, Error: {Error}", 
                                paymentIntentId, result.message);
                        }
                    }
                }
            }

            return new OkResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error processing payment webhook");
            return new StatusCodeResult(500);
        }
    }

    private class PayMongoWebhookPayload
    {
        public PayMongoWebhookData? Data { get; set; }
    }

    private class PayMongoWebhookData
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public PayMongoWebhookAttributes? Attributes { get; set; }
    }

    private class PayMongoWebhookAttributes
    {
        public PayMongoWebhookPaymentIntentData? Data { get; set; }
    }

    private class PayMongoWebhookPaymentIntentData
    {
        public string? Id { get; set; }
        public PayMongoWebhookPaymentIntentAttributes? Attributes { get; set; }
    }

    private class PayMongoWebhookPaymentIntentAttributes
    {
        public string? Status { get; set; }
        public long? Amount { get; set; }
        public string? Currency { get; set; }
    }
}

