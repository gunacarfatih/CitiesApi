
using System.Text;
using CitiesApi.Configurations;
using CitiesApi.Data;
using CitiesApi.Middlewares;
using CitiesApi.Models;
using CitiesApi.Services;
using CitiesApi.Responses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ReusableCaching.Extensions;
using Serilog;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

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
app.UseStatusCodePages(async context =>
{
    var response = new BaseResponse<object>
    {
        Success = false,
        Message = context.HttpContext.Response.StatusCode == 404
            ? "İstenen adres bulunamadı."
            : "İstek işlenirken bir hata oluştu.",
        Status = context.HttpContext.Response.StatusCode,
        ErrorCode = context.HttpContext.Response.StatusCode == 404
            ? "ENDPOINT_NOT_FOUND"
            : "HTTP_ERROR",
        TraceId = context.HttpContext.TraceIdentifier
    };

    context.HttpContext.Response.ContentType = "application/json";

    await context.HttpContext.Response.WriteAsJsonAsync(response);
});

// Correlation ID
app.UseMiddleware<CorrelationIdMiddleware>();

// Global Exception Handling
app.UseMiddleware<GlobalExceptionMiddleware>();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serilog Request Logging
app.UseSerilogRequestLogging();

// HTTPS
app.UseHttpsRedirection();

// Authentication ve Authorization
app.UseAuthentication();
app.UseAuthorization();

// Controllers
app.MapControllers();

app.Run();
