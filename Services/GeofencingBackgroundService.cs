using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace BiketaBai.Services;

public class GeofencingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2); // Check every 2 minutes for accurate reminders

    public GeofencingBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Geofencing background service started. Monitoring interval: {Interval} minutes", _checkInterval.TotalMinutes);

        // Wait a bit before first run to ensure app is fully started
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var geofencingService = scope.ServiceProvider.GetRequiredService<GeofencingService>();
                    var smsService = scope.ServiceProvider.GetRequiredService<SmsService>();
                    
                    // Verify services are available
                    if (geofencingService == null)
                    {
                        Log.Error("GeofencingService is null in background service scope");
                    }
                    if (smsService == null)
                    {
                        Log.Error("SmsService is null in background service scope");
                    }
                    
                    Log.Debug("Geofencing background service: Starting monitoring cycle");
                    await geofencingService.MonitorActiveBookingsAsync();
                    Log.Debug("Geofencing background service: Monitoring cycle completed");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Geofencing background service is stopping.");
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in geofencing background service: {ErrorMessage}. Stack trace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                // Wait a bit before retrying
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        Log.Information("Geofencing background service stopped.");
    }
}

