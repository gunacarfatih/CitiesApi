
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CitiesApi.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Beklenmeyen bir hata oluştu."
                );
                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();

                context.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;

                context.Response.ContentType =
                    "application/json";

                var response = new
                {
                    statusCode = 500,
                    message = "Beklenmeyen bir hata oluştu.",
                    success = false,
                    traceId = context.TraceIdentifier
                };

                await context.Response.WriteAsJsonAsync(response);
            }
        }
    }
}
