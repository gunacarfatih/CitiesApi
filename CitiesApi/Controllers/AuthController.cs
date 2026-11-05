using CitiesApi.Models;
using CitiesApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using CitiesApi.Data;
using Microsoft.AspNetCore.Mvc;
using CitiesApi.Responses;

namespace CitiesApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly TokenService _tokenService;

        public AuthController(
            ApplicationDbContext context,
            IPasswordHasher<User> passwordHasher,
            TokenService tokenService)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(
            [FromBody] RegisterRequest request,
            CancellationToken cancellationToken)
        {
            var usernameExists = await _context.Users
                .AnyAsync(
                    u => u.UserName == request.UserName,
                    cancellationToken);

            if (usernameExists)
            {
                return BadRequest(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Bu kullanıcı adı zaten kullanılıyor.",
                    Status = StatusCodes.Status400BadRequest,
                    ErrorCode = "USERNAME_ALREADY_EXISTS",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var user = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                Name = request.Name,
                Surname = request.Surname,
                IsActive = true,
                CreateDate = DateTime.UtcNow
            };

            user.Password = _passwordHasher.HashPassword(
                user,
                request.Password);

            _context.Users.Add(user);

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new BaseResponse<object>
            {
                Success = true,
                Message = "Kullanıcı başarıyla oluşturuldu.",
                Status = StatusCodes.Status200OK,
                Data = new
                {
                    UserId = user.Id,
                    Username = user.UserName,
                    Email = user.Email
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.UserName == request.UserName,
                    cancellationToken);

            if (user == null || !user.IsActive)
            {
                return Unauthorized(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Kullanıcı adı veya şifre hatalı.",
                    Status = StatusCodes.Status401Unauthorized,
                    ErrorCode = "INVALID_CREDENTIALS",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var passwordResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.Password,
                request.Password);

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new BaseResponse<object>
                {
                    Success = false,
                    Message = "Kullanıcı adı veya şifre hatalı.",
                    Status = StatusCodes.Status401Unauthorized,
                    ErrorCode = "INVALID_CREDENTIALS",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            var token = _tokenService.CreateToken(user);

            return Ok(new BaseResponse<object>
            {
                Success = true,
                Message = "Giriş başarılı.",
                Status = StatusCodes.Status200OK,
                Data = new
                {
                    UserId = user.Id,
                    Username = user.UserName,
                    AccessToken = token
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }
}
