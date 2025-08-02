using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PdfQAApp.Middleware
{
    // Supporting classes
    public class ErrorResponse
    {
        public ErrorDetails Error { get; set; } = new();
    }

    public class ErrorDetails
    {
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
            catch (Microsoft.AspNetCore.Http.BadHttpRequestException badHttpEx)
            {
                _logger.LogError(badHttpEx, "Bad HTTP request: {Message}", badHttpEx.Message);
                await HandleBadHttpRequestExceptionAsync(context, badHttpEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleBadHttpRequestExceptionAsync(HttpContext context, Microsoft.AspNetCore.Http.BadHttpRequestException exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            var response = new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Message = "Bad HTTP Request: " + exception.Message,
                    Type = nameof(Microsoft.AspNetCore.Http.BadHttpRequestException),
                    Timestamp = DateTime.UtcNow
                }
            };

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var response = new ErrorResponse
            {
                Error = new ErrorDetails
                {
                    Message = exception.Message,
                    Type = exception.GetType().Name,
                    Timestamp = DateTime.UtcNow
                }
            };

            context.Response.StatusCode = exception switch
            {
                FileNotFoundException => (int)HttpStatusCode.NotFound,
                ArgumentException => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                TimeoutException => (int)HttpStatusCode.RequestTimeout,
                _ => (int)HttpStatusCode.InternalServerError
            };

            // Customize known exception messages
            if (exception is FileNotFoundException)
                response.Error.Message = "File not found";
            else if (exception is ArgumentException)
                response.Error.Message = "Invalid argument provided";
            else if (exception is UnauthorizedAccessException)
                response.Error.Message = "Unauthorized access";
            else if (exception is TimeoutException)
                response.Error.Message = "Request timeout";
            else
                response.Error.Message = "An internal server error occurred";

            var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
