using System.Net;
using System.Text.Json;
using EquiLink.Api.Features.Funds.Commands;

namespace EquiLink.Api.Middleware;

public class GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorType) = exception switch
        {
            DuplicateFundException => (HttpStatusCode.Conflict, "DuplicateFund"),
            InvalidOperationException => (HttpStatusCode.BadRequest, "InvalidOperation"),
            _ => (HttpStatusCode.InternalServerError, "InternalError")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = errorType,
            message = exception.Message,
            statusCode = (int)statusCode
        };

        var json = JsonSerializer.Serialize(response);
        return context.Response.WriteAsync(json);
    }
}
