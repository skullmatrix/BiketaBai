using Microsoft.EntityFrameworkCore;
using BiketaBai.Data;
using BiketaBai.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/biketabai-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorPages();

// Configure MySQL Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BiketaBaiDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Configure Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60); // 60 minutes session
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Allow HTTP in development
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Allow HTTP in development
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
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
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

app.Run();

