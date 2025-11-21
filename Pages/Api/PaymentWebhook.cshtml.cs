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

            // Verify webhook token (Xendit sends webhook token in headers)
            var webhookToken = _configuration["AppSettings:XenditWebhookToken"];
            var xCallbackToken = Request.Headers["X-Callback-Token"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(webhookToken) && xCallbackToken != webhookToken)
            {
                Log.Warning("Invalid webhook token");
                return new UnauthorizedResult();
            }

            // Parse webhook payload (Xendit format)
            var webhookData = JsonSerializer.Deserialize<XenditWebhookPayload>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhookData != null)
            {
                var invoiceId = webhookData.Id ?? webhookData.ExternalId?.Replace("booking_", "");
                var status = webhookData.Status?.ToUpper();
                var eventType = Request.Headers["X-Event"].FirstOrDefault() ?? "";

                Log.Information("Xendit webhook event: {EventType}, Invoice: {InvoiceId}, Status: {Status}", 
                    eventType, invoiceId, status);

                // Handle payment succeeded events
                if (status == "PAID" || status == "SETTLED" || eventType == "invoice.paid")
                {
                    if (!string.IsNullOrEmpty(invoiceId))
                    {
                        var result = await _paymentService.ConfirmGatewayPaymentAsync(invoiceId);
                        
                        if (result.success)
                        {
                            Log.Information("Payment confirmed via Xendit webhook: {InvoiceId}", invoiceId);
                            return new OkResult();
                        }
                        else
                        {
                            Log.Warning("Failed to confirm payment via webhook: {InvoiceId}, Error: {Error}", 
                                invoiceId, result.message);
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

    private class XenditWebhookPayload
    {
        public string? Id { get; set; }
        public string? ExternalId { get; set; }
        public string? Status { get; set; }
        public double? Amount { get; set; }
        public string? Currency { get; set; }
    }
}

