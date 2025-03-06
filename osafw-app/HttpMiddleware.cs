using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace osafw;

public class MyHandlerMiddleware
{

    // Must have constructor with this signature, otherwise exception at run time
    public MyHandlerMiddleware(RequestDelegate next)
    {
        // This is an HTTP Handler, so no need to store next
    }

    public async Task Invoke(HttpContext context)
    {
        HttpRequest request = context.Request;
        HttpResponse response = context.Response;
        // preflight request
        if (request.Method.ToUpper() == "OPTIONS")
        {
            response.Clear();

            // Set allowed method And headers
            response.Headers.AccessControlAllowMethods = "GET, POST, PUT, PATCH, DELETE, OPTIONS";

            // allow any custom headers
            Microsoft.Extensions.Primitives.StringValues access_control_request_headers = new("");
            bool is_access_control_request_headers =
                request.Headers.TryGetValue("Access-Control-Request-Headers", out access_control_request_headers);

            if (is_access_control_request_headers)
                response.Headers.AccessControlAllowHeaders = access_control_request_headers;

            // response.Headers.AccessControlAllowHeaders = "*";
            // response.Headers.AccessControlExposeHeaders = "*";

            // allow credentials
            response.Headers.AccessControlAllowCredentials = "true";

            // Set allowed origin
            /*Dim origin = context.Request.Headers("Origin")
            If Not IsNothing(origin) Then
                response.Headers.AccessControlAllowOrigin = origin;
            Else
                response.Headers.AccessControlAllowOrigin = "*";
            End If*/

            // end request
            //context.RequestServices.CompleteRequest()
        }

        // Windows Authentication support
        if (!context.User.Identity.IsAuthenticated && context.Request.Path.ToString().ToLower().StartsWith("/winlogin"))
        {
            //if not authenticated and win login requested - send challenge to the browser
            await context.ChallengeAsync(Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme);
            return;
        }

        await Task.Run(() =>
        {
            FW.run(context, Startup.Configuration);
        });
    }
}

public static class MyHandlerExtensions
{
    public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MyHandlerMiddleware>();
    }
}
