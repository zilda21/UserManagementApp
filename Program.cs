using Microsoft.EntityFrameworkCore;
using UserManagementApp.Data;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null)
    )
);

builder.Services.AddControllers();
builder.Services.AddCors(o => o.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors("AllowAll");
app.UseStaticFiles();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/login.html"));


try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.OpenConnectionAsync();
    await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users(
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Email NVARCHAR(200) NOT NULL,
        Password NVARCHAR(255) NOT NULL,
        Status NVARCHAR(20) NOT NULL DEFAULT('unverified'),
        LastLogin DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT(getutcdate()),
        VerificationToken NVARCHAR(510) NULL
    );
    CREATE UNIQUE NONCLUSTERED INDEX IX_Users_Email ON dbo.Users(Email);
END
ELSE
BEGIN
    IF COL_LENGTH('dbo.Users','Password') IS NULL AND COL_LENGTH('dbo.Users','PasswordHash') IS NOT NULL
        EXEC sp_rename 'dbo.Users.PasswordHash', 'Password', 'COLUMN';

    IF COL_LENGTH('dbo.Users','Password') IS NULL
        ALTER TABLE dbo.Users ADD Password NVARCHAR(255) NOT NULL DEFAULT('');

    IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name='IX_Users_Email' AND object_id=OBJECT_ID('dbo.Users'))
        CREATE UNIQUE NONCLUSTERED INDEX IX_Users_Email ON dbo.Users(Email);
END
""");
    await db.Database.CloseConnectionAsync();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Database initialization failed at startup.");
}

app.Run();
