
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TripMind.Application.Exceptions;

namespace TripMind.API.Middleware
{
    public sealed class ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<ExceptionMiddleware> _logger = logger;

        public async Task InvokeAsync(HttpContext ctx)
        {
            try
            {
                await _next(ctx);
            }
            catch (Exception ex)
            {
                await HandleAsync(ctx, ex);
            }
        }

        private async Task HandleAsync(HttpContext ctx, Exception ex)
        {
            // 1. Check whether the response has already started.
            if (ctx.Response.HasStarted)
            {
                _logger.LogError(
                    ex,
                    "An exception occurred after the response started.");

                return;
            }

            int statusCode;
            string title;
            string errorCode;
            string detail;

            // 2. Handle known application exceptions.
            if (ex is AppException appEx)
            {
                statusCode = appEx.StatusCode;
                title = GetTitle(statusCode);
                errorCode = appEx.ErrorCode;
                detail = appEx.Message;

                _logger.LogWarning(
                    "Application error {ErrorCode} on {Method} {Path}",
                    errorCode,
                    ctx.Request.Method,
                    ctx.Request.Path);
            }
            else
            {
                // 3. Handle unexpected exceptions.
                statusCode = StatusCodes.Status500InternalServerError;
                title = "An unexpected error occurred.";
                errorCode = "INTERNAL_SERVER_ERROR";
                detail = "An internal server error occurred.";

                _logger.LogError(
                    ex,
                    "Unhandled exception on {Method} {Path}",
                    ctx.Request.Method,
                    ctx.Request.Path);
            }

            // 4. Build a standardized Problem Details response.
            var problem = new ProblemDetails
            {
                Type = $"https://httpstatuses.com/{statusCode}",
                Title = title,
                Status = statusCode,
                Detail = detail,
                Instance = ctx.Request.Path
            };

            problem.Extensions["errorCode"] = errorCode;
            problem.Extensions["traceId"] = ctx.TraceIdentifier;

            // 5. Send the response to the client.
            ctx.Response.Clear();
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "application/problem+json";

            await ctx.Response.WriteAsJsonAsync(
                problem);
        }

        private static string GetTitle(int statusCode)
        {
            return statusCode switch
            {
                400 => "Bad request.",
                401 => "Authentication required.",
                403 => "Access denied.",
                404 => "Resource not found.",
                409 => "Conflict.",
                503 => "Service unavailable.",
                _ => "Application error."
            };
        }
    }
}
