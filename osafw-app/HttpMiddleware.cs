using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace osafw;

/// <summary>
/// Custom HTTP middleware
/// Must have a constructor taking RequestDelegate next.
/// </summary>
public class MyHandlerMiddleware(RequestDelegate next, IConfiguration config)
{
    private readonly RequestDelegate _next = next;
    private readonly IConfiguration _config = config;

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // preflight request
        if (request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            // Clear the response
            response.Clear();

            // set allowed methods and headers
            response.Headers.AccessControlAllowMethods = "GET, POST, PUT, PATCH, DELETE, OPTIONS";

            // allow any custom headers
            if (request.Headers.TryGetValue("Access-Control-Request-Headers", out var acrh))
                response.Headers.AccessControlAllowHeaders = acrh;

            // allow credentials
            response.Headers.AccessControlAllowCredentials = "true";

            // (optional) set allowed origin dynamically
            // e.g. 
            // var origin = request.Headers["Origin"];
            // response.Headers.AccessControlAllowOrigin = string.IsNullOrEmpty(origin) ? "*" : origin;

            // No need to do anything else. We'll just let the request end
        }

        // Windows Authentication Support
        // If not authenticated and path is /winlogin => challenge
        if (!context.User.Identity.IsAuthenticated
            && request.Path.ToString().StartsWith("/winlogin", StringComparison.CurrentCultureIgnoreCase))
        {
            await context.ChallengeAsync(Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme);
            return;
        }

        // Call the FW "core" pipeline
        await Task.Run(() =>
        {
            FW.run(context, _config);
        });

        // If needed any post-processing, can be added here, after FW.run.
    }
}

/// <summary>
/// Extension method to add MyHandlerMiddleware in the pipeline
/// usage: app.UseMyHandler()
/// </summary>
public static class MyHandlerExtensions
{
    public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MyHandlerMiddleware>();
    }
}