using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Data;

var builder = WebApplication.CreateBuilder(args);

// Console logs help on Render
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// 1) Get connection string EXACTLY as stored (no edits)
string cs =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DefaultConnection")
    ?? "";

cs = cs.Replace("\r", "").Replace("\n", "").Trim();

// Optional: drop any junk BEFORE the first real key (Server= or Host=)
int i = cs.IndexOf("Server=", StringComparison.OrdinalIgnoreCase);
if (i < 0) i = cs.IndexOf("Host=", StringComparison.OrdinalIgnoreCase);
if (i > 0) cs = cs.Substring(i);

// Masked log so we can verify what we really use
string masked = System.Text.RegularExpressions.Regex.Replace(cs, @"Password=[^;]*", "Password=***", RegexOptions.IgnoreCase);
Console.WriteLine($"[ConnStr] {masked}");

// 2) Wire EF Core
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(cs, ServerVersion.AutoDetect(cs)));

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Static site
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

// Tiny diag endpoint to test DB quickly
app.MapGet("/api/diag/db", async ([FromServices] AppDbContext db) =>
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

app.MapGet("/", () => Results.Redirect("/login.html"));
app.MapControllers();
app.Run();
