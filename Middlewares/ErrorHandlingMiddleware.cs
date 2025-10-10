using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace studentsData.Middlewares
{
    // Throw DomainException when you want controlled errors (400/403 etc)
    public class DomainException : Exception
    {
        public int StatusCode { get; }
        public DomainException(string message, int statusCode = StatusCodes.Status400BadRequest) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (DomainException dex)
            {
                _logger.LogWarning(dex, "Domain error");
                await HandleExceptionAsync(context, dex.StatusCode, dex.Message, dex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                await HandleExceptionAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.", ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, int statusCode, string message, Exception ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var problem = new ProblemDetails
            {
                Title = message,
                Status = statusCode,
                Instance = context.Request?.Path
            };

            // Include stack trace/details only in development
            if (_env.IsDevelopment())
                problem.Detail = ex?.ToString();

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, options));
        }
    }

    public static class ErrorHandlingMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ErrorHandlingMiddleware>();
        }
    }
}
