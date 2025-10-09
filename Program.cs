using Microsoft.EntityFrameworkCore;
using UserManagementApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Console logs in Render
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// --- MySQL/MariaDB via Pomelo, with auto-detect + retry ---
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
         ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var sv = ServerVersion.AutoDetect(cs);
    opt.UseMySql(cs, sv, my => my.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(3),
        errorNumbersToAdd: null
    ));
});

builder.Services.AddControllers();

var app = builder.Build();

// In containers, don't force HTTPS if the platform doesn't configure it
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
app.UseDefaultFiles();   // serve wwwroot/index.html as default
app.UseStaticFiles();

app.MapControllers();

// quick redirect if you prefer:
app.MapGet("/", () => Results.Redirect("/login.html"));

// ---- DB self-test + bootstrap table (prints real error to Render logs) ----
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.OpenConnectionAsync();

    // Create table if it doesn't exist (MySQL/MariaDB-safe DDL)
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

// health probe to see exact DB status in browser/logs
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
