using CitiesApi.Models;
using CitiesApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using CitiesApi.Data;
using Microsoft.AspNetCore.Mvc;

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
            [FromBody] RegisterRequest request)
        {
            var usernameExists = await _context.Users
                .AnyAsync(u => u.UserName == request.UserName);

            if (usernameExists)
            {
                return BadRequest(new
                {
                    message = "Bu kullanıcı adı zaten kullanılıyor."
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

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Kullanıcı başarıyla oluşturuldu.",
                userId = user.Id,
                username = user.UserName,
                email = user.Email
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserName == request.UserName);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Kullanıcı adı veya şifre hatalı."
                });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new
                {
                    message = "Bu kullanıcı hesabı pasif durumda."
                });
            }

            var passwordResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.Password,
                request.Password);

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    message = "Kullanıcı adı veya şifre hatalı."
                });
            }

            var token = _tokenService.CreateToken(user);

            return Ok(new
            {
                message = "Giriş başarılı.",
                userId = user.Id,
                username = user.UserName,
                accessToken = token
            });
        }

    }
}
