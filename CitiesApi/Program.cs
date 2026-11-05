// .NET
using System.Text;

// ASP.NET Core
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

// Entity Framework Core
using Microsoft.EntityFrameworkCore;

// JWT
using Microsoft.IdentityModel.Tokens;

// Swagger
using Microsoft.OpenApi;

// Third-party
using ReusableCaching.Extensions;
using Serilog;

// Project
using CitiesApi.Configurations;
using CitiesApi.Data;
using CitiesApi.Middlewares;
using CitiesApi.Models;
using CitiesApi.Responses;
using CitiesApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins(
                "http://127.0.0.1:5500",
                "http://localhost:5500"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Serilog yapılandırması
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Controllers
builder.Services.AddControllers();

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .ToList();

        var response = new BaseResponse<object>
        {
            Success = false,
            Message = "Gönderilen veriler geçersiz.",
            Status = StatusCodes.Status400BadRequest,
            Errors = errors,
            ErrorCode = "VALIDATION_ERROR",
            TraceId = context.HttpContext.TraceIdentifier
        };

        return new BadRequestObjectResult(response);
    };
});

// JWT ayarları
var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT ayarları bulunamadı.");

if (Encoding.UTF8.GetByteCount(jwtSettings.SecretKey) < 32)
{
    throw new InvalidOperationException(
        "JWT SecretKey en az 32 byte olmalıdır.");
}

// JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtSettings.Issuer,

        ValidateAudience = true,
        ValidAudience = jwtSettings.Audience,

        ValidateLifetime = true,

        ValidateIssuerSigningKey = true,

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),

        ClockSkew = TimeSpan.FromSeconds(30)
    };

    options.Events = new JwtBearerEvents
    {
        // 401 - Kimlik doğrulama başarısız
        OnChallenge = async context =>
        {
            context.HandleResponse();

            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            context.Response.ContentType = "application/json";

            var response = new BaseResponse<object>
            {
                Success = false,
                Message = "Kimlik doğrulaması başarısız. Geçerli bir token gereklidir.",
                Status = StatusCodes.Status401Unauthorized,
                ErrorCode = "UNAUTHORIZED",
                TraceId = context.HttpContext.TraceIdentifier
            };

            await context.Response.WriteAsJsonAsync(response);
        },

        // 403 - Yetkisiz erişim
        OnForbidden = async context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            context.Response.ContentType = "application/json";

            var response = new BaseResponse<object>
            {
                Success = false,
                Message = "Bu kaynağa erişim yetkiniz bulunmuyor.",
                Status = StatusCodes.Status403Forbidden,
                ErrorCode = "FORBIDDEN",
                TraceId = context.HttpContext.TraceIdentifier
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    };
});

// JWT Settings ve servisler
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

builder.Services.AddScoped<TokenService>();

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Cache
builder.Services.AddReusableCaching(builder.Configuration);

// Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT token giriniz."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});


var app = builder.Build();

// 1. Correlation ID
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Serilog Request Logging
app.UseSerilogRequestLogging();

// 3. Global Exception Handling
app.UseMiddleware<GlobalExceptionMiddleware>();

// 4. Status Code Pages
app.UseStatusCodePages(async context =>
{
    var httpContext = context.HttpContext;
    var statusCode = httpContext.Response.StatusCode;

    var isNotFound = statusCode == StatusCodes.Status404NotFound;

    var response = new BaseResponse<object>
    {
        Success = false,
        Message = isNotFound
            ? "İstenen adres bulunamadı."
            : "İstek işlenirken bir hata oluştu.",

        Status = statusCode,

        ErrorCode = isNotFound
            ? "ENDPOINT_NOT_FOUND"
            : "HTTP_ERROR",

        TraceId = httpContext.TraceIdentifier
    };

    httpContext.Response.ContentType = "application/json";

    await httpContext.Response.WriteAsJsonAsync(response);
});

// 5. Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 6. HTTPS
app.UseHttpsRedirection();

// 7. CORS
app.UseCors("FrontendPolicy");

// 8. Authentication
app.UseAuthentication();

// 9. Authorization
app.UseAuthorization();

// 10. Controllers
app.MapControllers();

app.Run();