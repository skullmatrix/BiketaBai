using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace BiketaBai.Services;

public class GeofencingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(10); // Check every 10 minutes

    public GeofencingBackgroundService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("Geofencing background service started. Monitoring interval: {Interval} minutes", _checkInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var geofencingService = scope.ServiceProvider.GetRequiredService<GeofencingService>();
                    await geofencingService.MonitorActiveBookingsAsync();
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
                Log.Error(ex, "Error in geofencing background service");
                // Wait a bit before retrying
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        Log.Information("Geofencing background service stopped.");
    }
}

