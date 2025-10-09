using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Data;

var builder = WebApplication.CreateBuilder(args);

// ---- Connection string (supports appsettings.json OR env vars) ----
var cs =
    builder.Configuration.GetConnectionString("DefaultConnection") ??
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ??
    Environment.GetEnvironmentVariable("DefaultConnection") ??
    "";

// If someone pasted "DefaultConnection=Server=..." (or anything before server/host), keep only from the first server/host
cs = cs.Trim();
int idxServer = cs.IndexOf("server=", StringComparison.OrdinalIgnoreCase);
int idxHost   = cs.IndexOf("host=",   StringComparison.OrdinalIgnoreCase);
int idx = -1;
if (idxServer >= 0 && idxHost >= 0) idx = Math.Min(idxServer, idxHost);
else if (idxServer >= 0) idx = idxServer;
else if (idxHost   >= 0) idx = idxHost;
if (idx >= 0) cs = cs.Substring(idx);

// Basic sanity check
if (!cs.Contains("server=", StringComparison.OrdinalIgnoreCase) &&
    !cs.Contains("host=",   StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Invalid MySQL connection string: missing Server/Host.");
if (!cs.Contains("database=", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Invalid MySQL connection string: missing Database.");

// Log a safe version (mask password)
var safe = Regex.Replace(cs, @"(?i)\b(password|pwd)\s*=\s*[^;]*", "Password=***");
Console.WriteLine($"[ConnStr] Using MySQL: {safe}");

// ---- EF Core ----
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(cs, ServerVersion.AutoDetect(cs)));

builder.Services.AddControllersWithViews();

var app = builder.Build();

// ---- Pipeline ----
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection(); // optional on Render (HTTP only); uncomment locally if you want HTTPS redirect
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();

// Redirect root to login page
app.MapGet("/", ctx =>
{
    ctx.Response.Redirect("/login.html");
    return Task.CompletedTask;
});

// Simple DB diagnostic endpoint: GET /api/diag/db  -> "OK" or problem
app.MapGet("/api/diag/db", async (AppDbContext db) =>
{
    try
    {
        await db.Database.OpenConnectionAsync();
        await db.Database.CloseConnectionAsync();
        return Results.Ok("OK");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.GetBaseException().Message);
    }
});

app.Run();
