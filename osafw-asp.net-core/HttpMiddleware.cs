using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text;
using osafw_asp.net_core.fw;

namespace osafw_asp.net_core
{
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
            if (context.Request.Method.ToUpper() == "OPTIONS")
            {
                response.Clear();

                // Set allowed method And headers
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

                // allow any custom headers
                Microsoft.Extensions.Primitives.StringValues access_control_request_headers = new Microsoft.Extensions.Primitives.StringValues("");
                bool is_access_control_request_headers = 
                    request.Headers.TryGetValue("Access-Control-Request-Headers", out access_control_request_headers);
                
                if (is_access_control_request_headers) 
                {
                    response.Headers.Append("Access-Control-Allow-Headers", access_control_request_headers.ToString());
                }
                // response.AppendHeader("Access-Control-Allow-Headers", "*")
                // response.AppendHeader("Access-Control-Expose-Headers", "*")

                // allow credentials
                response.Headers.Add("Access-Control-Allow-Credentials", "true");

                // Set allowed origin
                /*Dim origin = context.Request.Headers("Origin")
                If Not IsNothing(origin) Then
                    response.AppendHeader("Access-Control-Allow-Origin", origin)
                Else
                    response.AppendHeader("Access-Control-Allow-Origin", "*")
                End If*/

                // end request
                //context.RequestServices.CompleteRequest()
            }

            FW.run(context, Startup.Configuration);
        }
    }

    public static class MyHandlerExtensions
    {
        public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MyHandlerMiddleware>();
        }
    }
}
