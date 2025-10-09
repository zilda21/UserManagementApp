using Microsoft.EntityFrameworkCore;
using UserManagementApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Console logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// --- connection string (read from env first, then appsettings) ---
string raw = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
           ?? builder.Configuration.GetConnectionString("DefaultConnection")
           ?? throw new InvalidOperationException("Missing 'DefaultConnection'.");

string Clean(string s)
{
    // remove CR/LF/TAB and trim – these cause the 'ion\n server' error
    return new string(s.Where(c => c != '\r' && c != '\n' && c != '\t').ToArray()).Trim();
}
var cs = Clean(raw);

// log a redacted version so we can see exactly what EF sees
try
{
    var redacted = System.Text.RegularExpressions.Regex.Replace(cs, @"Pwd=[^;]*", "Pwd=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    builder.Logging.CreateLogger("ConnStr").LogInformation("Using MySQL conn string: {Conn}", redacted);
}
catch { /* ignore logging problems */ }

// --- EF + Pomelo ---
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var sv = ServerVersion.AutoDetect(cs); // detects MariaDB 10.11.x on AlwaysData
    opt.UseMySql(cs, sv, my => my.EnableRetryOnFailure(5, TimeSpan.FromSeconds(3), null));
});

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/login.html"));

// DB check + create table if needed
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.OpenConnectionAsync();

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

// health endpoint so you can see the real DB error in browser/logs
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
