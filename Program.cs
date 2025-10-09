using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using UserManagementApp.Data;

var builder = WebApplication.CreateBuilder(args);

// ENV var wins; falls back to appsettings.json
var cs = Environment.GetEnvironmentVariable("DefaultConnection")
         ?? builder.Configuration.GetConnectionString("DefaultConnection")
         ?? "";

// Validate & sanitize (this throws immediately on bad keys like "ionServer")
try
{
    var csb = new MySqlConnectionStringBuilder(cs);
    // print a masked version to logs (won't mutate keys)
    var masked = new MySqlConnectionStringBuilder(csb.ConnectionString) { Password = "***" };
    Console.WriteLine("[ConnStr] " + masked.ConnectionString);
    cs = csb.ConnectionString; // normalized
}
catch (Exception ex)
{
    Console.WriteLine("❌ Invalid MySQL connection string: " + ex.Message);
    throw;
}

// EF Core + Pomelo for MySQL/MariaDB
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(cs, ServerVersion.AutoDetect(cs)));

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Ensure DB/tables exist on boot (simple demo style)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.EnsureCreated();
        Console.WriteLine("✅ Database ready.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ DB init failed: " + ex.GetBaseException().Message);
        throw;
    }
}

// static files + default to index/login
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// quick DB health check
app.MapGet("/api/diag/db", async (AppDbContext db) =>
{
    try
    {
        var ok = await db.Database.CanConnectAsync();
        return Results.Ok(new { canConnect = ok });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.GetBaseException().Message);
    }
});

// send "/" to login page
app.MapGet("/", ctx => { ctx.Response.Redirect("/login.html"); return Task.CompletedTask; });

app.Run();
