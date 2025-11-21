using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Services;
using Serilog;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Create logs directory if it doesn't exist
var isDevelopment = builder.Environment.IsDevelopment();
var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
Directory.CreateDirectory(logsPath);

// Create uploads directories if they don't exist
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
var uploadsPath = Path.Combine(wwwrootPath, "uploads");
Directory.CreateDirectory(Path.Combine(uploadsPath, "bikes"));
Directory.CreateDirectory(Path.Combine(uploadsPath, "id-documents"));
Directory.CreateDirectory(Path.Combine(uploadsPath, "profiles"));

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(logsPath, "biketabai-.txt"), rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorPages();

// Configure MySQL Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    // Use explicit MySQL version for production (more reliable than AutoDetect)
    var serverVersion = ServerVersion.Create(new Version(8, 0, 0), ServerType.MySql);
    
    builder.Services.AddDbContext<BiketaBaiDbContext>(options =>
    {
        options.UseMySql(connectionString, serverVersion, mysqlOptions =>
        {
            mysqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    });
}

// Configure Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60); // 60 minutes session
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.None : CookieSecurePolicy.Always; // HTTPS in production
    options.Cookie.SameSite = SameSiteMode.Lax; // More permissive for better compatibility
});

// Configure Authentication
builder.Services.AddAuthentication("BiketaBaiAuth")
    .AddCookie("BiketaBaiAuth", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60); // 60 minutes session
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.None : CookieSecurePolicy.Always; // HTTPS in production
        options.Cookie.SameSite = SameSiteMode.Lax; // More permissive for better compatibility
    });

builder.Services.AddAuthorization();

// Register application services
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<PointsService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<RatingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<UserProfileService>();
builder.Services.AddScoped<BookingManagementService>();
builder.Services.AddHttpClient<AddressValidationService>();
builder.Services.AddScoped<AddressValidationService>();
builder.Services.AddHttpClient<OpenStreetMapService>();
builder.Services.AddScoped<OpenStreetMapService>();
builder.Services.AddScoped<IdValidationService>();
builder.Services.AddHttpClient<SmsService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddHttpClient<PaymentGatewayService>();
builder.Services.AddScoped<PaymentGatewayService>();

// Add HttpContextAccessor for accessing HttpContext in services
builder.Services.AddHttpContextAccessor();

// Add AntiForgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // In production, log errors and show user-friendly page
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/html";

            var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;

            // Log the error with details
            if (exception != null)
            {
                Log.Error(exception, "Unhandled exception: {Message}. Path: {Path}", 
                    exception.Message, exceptionHandlerPathFeature?.Path ?? "Unknown");
            }

            // Redirect to error page
            context.Response.Redirect("/Error");
        });
    });
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// HTTPS redirection - only in development (Railway handles HTTPS at proxy level)
// In production, Railway terminates HTTPS, so we don't need to redirect
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Create database and apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<BiketaBaiDbContext>();
        
        // Apply migrations
        await context.Database.MigrateAsync();
        Log.Information("Database migration completed successfully");
        
        // Seed database with initial data
        var seeder = new BiketaBai.Data.DatabaseSeeder(context);
        await seeder.SeedAsync();
        Log.Information("Database seeding completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while migrating or seeding the database");
    }
}

Log.Information("Starting Bike Ta Bai application");

// Railway provides PORT environment variable, but if ASPNETCORE_URLS is set, use that
// Otherwise, configure based on PORT env var
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    // Set ASPNETCORE_URLS if PORT is provided but ASPNETCORE_URLS is not set
    Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://0.0.0.0:{port}");
    Log.Information($"Configured ASPNETCORE_URLS from PORT environment variable: http://0.0.0.0:{port}");
}

app.Run();

