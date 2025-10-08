using Microsoft.EntityFrameworkCore;
using UserManagementApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Get the raw connection string once
var cs = builder.Configuration.GetConnectionString("DefaultConnection")!;

// Pomelo + MariaDB with AutoDetect and transient retry
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        cs,
        ServerVersion.AutoDetect(cs),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)
    )
);

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Try the DB connection at startup so errors go to logs
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.OpenConnectionAsync();
        await db.Database.CloseConnectionAsync();

        // If you're using EF migrations and want the app to create tables automatically:
        // await db.Database.MigrateAsync();
        // Otherwise you already created the Users table manually — that’s fine too.
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database connection failed. Check connection string and DB user rights.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Optional: makes "/" map to /index.html in wwwroot
app.UseDefaultFiles();              // <= optional

// Required: actually serves wwwroot files (register.html, css, js, etc.)
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapGet("/healthz", async (AppDbContext db)
    => await db.Database.CanConnectAsync() ? Results.Ok("ok") : Results.Problem("db"));

app.Run();

