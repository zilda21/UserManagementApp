using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Basic console logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// 1) Read connection string (ENV wins, then appsettings)
string raw =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing 'DefaultConnection' connection string.");

static string Clean(string s)
{
    // Remove CR/LF/TAB that can sneak in from copy/paste and break parsing
    return new string(s.Where(c => c != '\r' && c != '\n' && c != '\t').ToArray()).Trim();
}
var cs = Clean(raw);

// Log a redacted version so you can see exactly what EF sees
try
{
    var redacted = Regex.Replace(cs, @"(?i)(Pwd|Password)=[^;]*", "$1=***");
    Console.WriteLine($"[ConnStr] Using MySQL conn string: {redacted}");
}
catch { /* best effort */ }

// 2) EF Core + Pomelo for MariaDB
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var serverVersion = ServerVersion.AutoDetect(cs);
    opt.UseMySql(cs, serverVersion, my => my.EnableRetryOnFailure(5, TimeSpan.FromSeconds(3), null));
});

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/login.html"));

// 3) DB connectivity + bootstrap table if missing
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.OpenConnectionAsync();

    // Create Users table if it doesn't exist (idempotent)
    await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS `Users` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `Name` VARCHAR(100) NOT NULL,
  `Email` VARCHAR(200) NOT NULL,
  `Password` VARCHAR(255) NOT NULL,
  `Status` VARCHAR(20) NOT NULL DEFAULT 'unverified',
  `LastLogin` DATETIME(6) NULL,
  `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `VerificationToken` VARCHAR(510) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Users_Email` (`Email`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
");

    await db.Database.CloseConnectionAsync();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "❌ Database initialization/connection failed at startup.");
}

// 4) Health endpoint to see the real DB error in browser/logs
app.MapGet("/api/diag/db", async (AppDbContext db) =>
{
    try
    {
        await db.Database.OpenConnectionAsync();
        await db.Database.CloseConnectionAsync();
        return Results.Ok("DB OK");
    }
    catch (Exception ex)
    {
        return Results.Problem("DB ERROR: " + ex.GetBaseException().Message, statusCode: 500);
    }
});

app.Run();
