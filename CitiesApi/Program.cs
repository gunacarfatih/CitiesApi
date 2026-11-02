
using CitiesApi.Data;
using CitiesApi.Services;
using CitiesApi.Middlewares;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Serilog;
using ReusableCaching.Extensions;

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

builder.Services.AddReusableCaching(builder.Configuration);

// Redis Connection
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("Redis");

    return ConnectionMultiplexer.Connect(connectionString!);
});

// Distributed Lock
builder.Services.AddSingleton<RedisDistributedLock>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Global Exception Handling
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
