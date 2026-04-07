using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace WonderWatch.Web.Middleware
{
    /// <summary>
    /// Intercepts all unhandled exceptions in the HTTP pipeline.
    /// Logs the full exception securely via Serilog and returns a sanitized, 
    /// context-aware response (JSON for APIs/AJAX, HTML redirect for standard MVC requests).
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
                // 1. Securely log the full exception and stack trace server-side
                _logger.LogError(ex, "An unhandled system anomaly occurred during the request. TraceId: {TraceId}", context.TraceIdentifier);

                // 2. Handle the response gracefully
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var traceId = context.TraceIdentifier;

            // Determine if the request is an API call or an AJAX request from our frontend JS
            var isApiRequest = context.Request.Path.StartsWithSegments("/api") ||
                               context.Request.Path.StartsWithSegments("/checkout/create-order") ||
                               context.Request.Path.StartsWithSegments("/checkout/verify") ||
                               (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest");

            if (isApiRequest)
            {
                // Return a structured JSON response for frontend JS to parse
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                var response = new
                {
                    success = false,
                    error = "A critical system anomaly occurred. Our artisans have been notified.",
                    traceId = traceId
                };

                var jsonResponse = JsonSerializer.Serialize(response);
                await context.Response.WriteAsync(jsonResponse);
            }
            else
            {
                // For standard MVC page requests, redirect to the branded "Lost in Time" Error page
                // We pass the traceId so it can be displayed to the user for support purposes
                context.Response.Redirect($"/Home/Error?traceId={traceId}");
            }
        }
    }
}