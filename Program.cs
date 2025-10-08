using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using UserManagementApp.Data;

var builder = WebApplication.CreateBuilder(args);

// MySQL / MariaDB (AlwaysData ~ MariaDB 10.11.x)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(10, 11, 14)),
        my => my.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null)
    )
);

builder.Services.AddControllers();
builder.Services.AddCors(o => o.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors("AllowAll");

// serve files from wwwroot (index.html / login.html etc.)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// optional: hard redirect root to login if you prefer
// app.MapGet("/", () => Results.Redirect("/login.html"));

// ensure DB + tables exist
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Database initialization failed at startup.");
}

app.Run();
