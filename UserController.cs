using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagementApp.Attributes;
using UserManagementApp.Data;
using AppUser = UserManagementApp.Models.User;

namespace UserManagementApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UserController(AppDbContext db) => _db = db;

        public record RegisterDto(string Name, string Email, string Password);
        public record LoginDto(string Email, string Password);
        public class IdsDto { public List<int> Ids { get; set; } = new(); }

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            var baseEx = ex.GetBaseException();
            if (baseEx is Microsoft.Data.SqlClient.SqlException sqlEx)
                return sqlEx.Number == 2627 || sqlEx.Number == 2601;
            return false;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (dto is null ||
                string.IsNullOrWhiteSpace(dto.Name) ||
                string.IsNullOrWhiteSpace(dto.Email) ||
                string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { message = "Name, email and password are required." });

            var email = dto.Email.Trim().ToLowerInvariant();

            if (await _db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "Email already registered." });

            var user = new AppUser
            {
                Name = dto.Name.Trim(),
                Email = email,
                Password = dto.Password,
                Status = "unverified",
                CreatedAt = DateTime.UtcNow,
                VerificationToken = Guid.NewGuid().ToString("N")
            };

            _db.Users.Add(user);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return Conflict(new { message = "Email already registered." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Registration failed: " + ex.GetBaseException().Message });
            }

            var verifyUrl = $"{Request.Scheme}://{Request.Host}/verify.html?token={user.VerificationToken}";
            return Ok(new
            {
                message = "Registered successfully. Please verify your email.",
                verificationToken = user.VerificationToken,
                verifyUrl
            });
        }

        [HttpGet("verify")]
        public async Task<IActionResult> Verify([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { message = "Missing token." });

            var user = await _db.Users.FirstOrDefaultAsync(u => u.VerificationToken == token);
            if (user == null)
                return NotFound(new { message = "Invalid token." });

            user.VerificationToken = null;
            user.Status = "active";
            await _db.SaveChangesAsync();

            return Redirect("/login.html");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { message = "Email and password are required." });

            var email = dto.Email.Trim().ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || user.Password != dto.Password)
                return Unauthorized(new { message = "Invalid email or password." });

            if (user.Status == "blocked")
                return Unauthorized(new { message = "Account is blocked." });

            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            Response.Cookies.Append("uid", user.Id.ToString(), new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(1)
            });

            return Ok(new { message = "Login successful", status = user.Status, id = user.Id });
        }

        [RequireUser]
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Users
                .OrderBy(u => u.Id)
                .Select(u => new { u.Id, u.Name, u.Email, u.Status, u.LastLogin, u.CreatedAt })
                .ToListAsync();

            return Ok(list);
        }

        [RequireUser]
        [HttpPost("block")]
        public async Task<IActionResult> Block([FromBody] IdsDto dto)
        {
            if (dto?.Ids == null || dto.Ids.Count == 0)
                return BadRequest(new { message = "No user ids provided." });

            var users = await _db.Users.Where(u => dto.Ids.Contains(u.Id)).ToListAsync();
            bool blockedSelf = false;

            if (Request.Cookies.TryGetValue("uid", out var uidStr) &&
                int.TryParse(uidStr, out var currentUserId))
            {
                foreach (var u in users)
                {
                    u.Status = "blocked";
                    if (u.Id == currentUserId)
                        blockedSelf = true;
                }
            }
            else
            {
                foreach (var u in users)
                    u.Status = "blocked";
            }

            await _db.SaveChangesAsync();

            if (blockedSelf)
            {
                Response.Cookies.Delete("uid");
                return StatusCode(440, new { message = "You blocked your own account. Logging out..." });
            }

            return Ok(new { message = "Selected users blocked." });
        }

        [RequireUser]
        [HttpPost("unblock")]
        public async Task<IActionResult> Unblock([FromBody] IdsDto dto)
        {
            if (dto?.Ids == null || dto.Ids.Count == 0)
                return BadRequest(new { message = "No user ids provided." });

            var users = await _db.Users.Where(u => dto.Ids.Contains(u.Id)).ToListAsync();

            foreach (var u in users)
            {
                if (!string.IsNullOrEmpty(u.VerificationToken))
                    u.Status = "unverified";
                else
                    u.Status = "active";
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Selected users unblocked (verification preserved)." });
        }

      [RequireUser]
[HttpPost("delete-unverified")]
public async Task<IActionResult> DeleteUnverified([FromBody] IdsDto dto)
{
   
    if (dto?.Ids == null || dto.Ids.Count == 0)
        return BadRequest(new { message = "Select at least one user." });

   
    var toDelete = await _db.Users
        .Where(u => dto.Ids.Contains(u.Id) && u.Status == "unverified")
        .ToListAsync();

    if (toDelete.Count == 0)
        return NoContent();

    _db.Users.RemoveRange(toDelete);
    await _db.SaveChangesAsync();
    return Ok(new { deleted = toDelete.Select(u => u.Id).ToArray() });
}


        [RequireUser]
        [HttpPost("delete")]
        public async Task<IActionResult> Delete([FromBody] IdsDto body, [FromQuery] string? ids = null, [FromQuery] int? id = null)
        {
            var allIds = new List<int>();
            if (body?.Ids != null) allIds.AddRange(body.Ids);
            if (id.HasValue) allIds.Add(id.Value);

            if (!string.IsNullOrWhiteSpace(ids))
                foreach (var s in ids.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    if (int.TryParse(s.Trim(), out var v)) allIds.Add(v);

            allIds = allIds.Distinct().ToList();
            if (allIds.Count == 0)
                return BadRequest(new { message = "No user ids provided." });

            var toDelete = await _db.Users.Where(u => allIds.Contains(u.Id)).ToListAsync();
            _db.Users.RemoveRange(toDelete);
            await _db.SaveChangesAsync();

            return Ok(new { message = $"Deleted {toDelete.Count} user(s).", deletedIds = toDelete.Select(u => u.Id) });
        }
    }
}
