
using Microsoft.AspNetCore.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EnterpriseKnowledge.API.Data;
using EnterpriseKnowledge.API.Models;
using EnterpriseKnowledge.API.DTOs;

namespace EnterpriseKnowledge.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AuthController(
            AppDbContext context,
            IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        // POST: api/Auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register(
            RegisterRequestDto request)
        {
            string email = request.Email.Trim().ToLowerInvariant();

            bool emailExists = await _context.Users
                .AnyAsync(u => u.Email == email);

            if (emailExists)
            {
                return BadRequest(new
                {
                    message = "An account with this email already exists."
                });
            }

            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = email,
                Role = "Employee"
            };

            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password
            );

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Registration successful.",
                user = new
                {
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.Role
                }
            });
        }

        // POST: api/Auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            LoginRequestDto request)
        {
            string email = request.Email.Trim().ToLowerInvariant();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            var passwordResult =
                _passwordHasher.VerifyHashedPassword(
                    user,
                    user.PasswordHash,
                    request.Password
                );

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    user.UserId.ToString()
                ),

                new Claim(ClaimTypes.Name, user.FullName),

                new Claim(ClaimTypes.Email, user.Email),

                new Claim(ClaimTypes.Role, user.Role)
            };

            var configuration = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>();

            var jwtKey = configuration["Jwt:Key"]!;
            var jwtIssuer = configuration["Jwt:Issuer"]!;
            var jwtAudience = configuration["Jwt:Audience"]!;

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler()
                .WriteToken(token);

            return Ok(new
            {
                message = "Login successful.",
                token = tokenString,

                user = new
                {
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.Role
                }
            });
        }

        // GET: api/Auth/profile
        [Authorize]
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            return Ok(new
            {
                message = "You are authorized!",

                email = User.FindFirst(
                    ClaimTypes.Email)?.Value,

                role = User.FindFirst(
                    ClaimTypes.Role)?.Value
            });
        }

        // GET: api/Auth/admin/users
        // Admin-only: View all users
        [Authorize(Roles = "Admin")]
        [HttpGet("admin/users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .OrderBy(u => u.UserId)
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                totalUsers = users.Count,
                users
            });
        }

        // PUT: api/Auth/admin/users/2/role
        // Admin-only: Change user role
        [Authorize(Roles = "Admin")]
        [HttpPut("admin/users/{userId}/role")]
        public async Task<IActionResult> UpdateUserRole(
            int userId,
            [FromBody] string newRole)
        {
            if (string.IsNullOrWhiteSpace(newRole))
            {
                return BadRequest(new
                {
                    message = "Role is required."
                });
            }

            newRole = newRole.Trim();

            if (newRole != "Admin" && newRole != "Employee")
            {
                return BadRequest(new
                {
                    message = "Role must be Admin or Employee."
                });
            }

            string? currentUserIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(currentUserIdClaim, out int currentUserId))
            {
                return Unauthorized();
            }

            if (userId == currentUserId)
            {
                return BadRequest(new
                {
                    message = "You cannot change your own role."
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return NotFound(new
                {
                    message = "User not found."
                });
            }

            if (user.Role == "Admin" && newRole == "Employee")
            {
                int adminCount = await _context.Users
                    .CountAsync(u => u.Role == "Admin");

                if (adminCount <= 1)
                {
                    return BadRequest(new
                    {
                        message = "Cannot remove the last Admin account."
                    });
                }
            }

            user.Role = newRole;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "User role updated successfully.",
                user.UserId,
                user.FullName,
                user.Email,
                user.Role
            });
        }
    }
}
