using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
#if isWindowsAuth
using Microsoft.AspNetCore.Authentication.Negotiate;
#endif
using System;

namespace osafw;

public static class Program
{
    private const bool ALLOW_PLAINTEXT_DP_KEYS = false;

    public static void Main(string[] args)
    {
        // In .NET 6+ the recommended pattern is the "WebApplication.CreateBuilder" approach
        var builder = WebApplication.CreateBuilder(args);

#if isSentry
        builder.WebHost.UseSentry();
#endif

        // read the environment settings
        var settings = FwConfig.settingsForEnvironment(builder.Configuration);

        var isDevelopmentEnv = settings["IS_DEV"].toBool();

        // Retrieve main DB connection info
        var dbSection = settings["db"] as FwDict ?? [];
        var mainDB = dbSection["main"] as FwDict ?? [];
        var connStr = mainDB["connection_string"].toStr();
        var dbType = mainDB["type"].toStr();
        if (string.IsNullOrEmpty(connStr) || string.IsNullOrEmpty(dbType))
            throw new ApplicationException("Main DB configuration is missing");

        // Site name used in data-protection app name
        var appName = settings["SITE_NAME"].toStr();
        if (string.IsNullOrEmpty(appName))
            appName = "osafw";

        //-------------------------------
        // Service registration
        //-------------------------------
        // Response Compression
        builder.Services.AddResponseCompression(options =>
        {
            // The original Startup.cs disabled HTTPS compression in dev to avoid intermittent issues
            options.EnableForHttps = !isDevelopmentEnv;
            options.MimeTypes =
            [
                    "text/plain",
                    "text/html",
                    "text/css",
                    "application/javascript",
                    "text/javascript",
                    "text/xml",
                    "text/csv",
                    "application/json",
                    "image/svg+xml"
                ];
        });

        // Data Protection
        // repository used for keys storage in the DB
        var repository = new FwKeysXmlRepository(new DB(connStr, dbType, "main"));
        var dataProtectionBuilder = builder.Services.AddDataProtection().SetApplicationName(appName);
        if (OperatingSystem.IsWindows())
            dataProtectionBuilder.ProtectKeysWithDpapi(protectToLocalMachine: true);
        else if (!ALLOW_PLAINTEXT_DP_KEYS)
            throw new ApplicationException("Data Protection key encryption requires Windows DPAPI or an explicit local/dev plaintext fallback.");
        builder.Services.Configure<KeyManagementOptions>(options =>
            {
                options.XmlRepository = repository; // i.e. "PersistKeysToCustomXmlRepository"
            });

        // Session rows use the provider-specific cache backing store for the main DB.
        builder.Services.AddFwSessionCache(connStr, dbType);

        // Form upload/limits
        builder.Services.Configure<FormOptions>(options =>
        {
            // options.ValueLengthLimit = 104857600; // 100MB
            // options.MultipartBodyLengthLimit = 1073741824; // 1GB
        });

        // IIS
        builder.Services.Configure<IISServerOptions>(options =>
        {
            options.AllowSynchronousIO = false;
        });

        // Session
        builder.Services.AddSession(options =>
        {
            options.Cookie.IsEssential = true;
            // if configured, set idle timeout
            var idleTimeout = builder.Configuration.GetValue<int>("sessionIdleTimeout");
            if (idleTimeout > 0)
                options.IdleTimeout = TimeSpan.FromSeconds(idleTimeout);

            var cookieHttpOnlySetting = builder.Configuration.GetValue<bool?>("cookieHttpOnly");
            if (cookieHttpOnlySetting != null)
                options.Cookie.HttpOnly = cookieHttpOnlySetting.Value;
        });

#if isWindowsAuth
        builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
#endif

        // Memory cache
        builder.Services.AddMemoryCache();

#if isFwCronService
        builder.Services.AddHostedService<FwCronService>();
#endif

        // Build the WebApplication
        var app = builder.Build();

        //-------------------------------
        // Configure the middleware pipeline
        //-------------------------------

        FwCache.MemoryCache = app.Services.GetRequiredService<IMemoryCache>();

        // Detailed exception pages are allowed only when framework dev mode is enabled.
        if (isDevelopmentEnv)
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    if (!context.Response.HasStarted)
                    {
                        context.Response.Clear();
                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        context.Response.ContentType = "text/plain; charset=utf-8";
                        await context.Response.WriteAsync(FW.GENERIC_SERVER_ERROR_MESSAGE);
                    }
                });
            });

            // In production - redirect HTTP to HTTPS, optionally UseHsts if needed
            app.UseHttpsRedirection();
            // app.UseHsts(); // if strict-transport
        }

        // Response compression
        app.UseResponseCompression();

        // Aggressive caching of static files
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                var headers = ctx.Context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromDays(365)
                };
            }
        });

        // Sessions
        app.UseSession();

        // Cookie policy
        app.UseCookiePolicy(new CookiePolicyOptions
        {
            CheckConsentNeeded = _ => false,
            HttpOnly = HttpOnlyPolicy.Always,
            MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
            //  Strict => external links to app won't keep session
            //  Lax => external links to app will keep session
            Secure = CookieSecurePolicy.SameAsRequest,
            OnAppendCookie = context =>
            {
                context.IssueCookie = true;
            },
            OnDeleteCookie = context =>
            {
                // no-op
            }
        });

#if isWindowsAuth
        app.UseAuthentication();
#endif

        // Security headers
        app.Use(async (context, next) =>
        {
            // default content type
            context.Response.ContentType = "text/html; charset=utf-8";

            // security headers
            context.Response.Headers.XContentTypeOptions = "NOSNIFF";
            context.Response.Headers.XFrameOptions = "DENY"; // or SAMEORIGIN - allows site iframes
            context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "master-only");
            context.Response.Headers.XXSSProtection = "1; mode=block";

            await next();
        });

        // Final handler
        app.Run(async context =>
        {
            var request = context.Request;
            var response = context.Response;

            // CORS preflight (OPTIONS)
            if (HttpMethods.IsOptions(request.Method))
            {
                response.Clear();
                response.Headers.AccessControlAllowMethods = "GET, POST, PUT, PATCH, DELETE, OPTIONS";
                if (request.Headers.TryGetValue("Access-Control-Request-Headers", out var acrh))
                    response.Headers.AccessControlAllowHeaders = acrh;
                response.Headers.AccessControlAllowCredentials = "true";
                // optionally dynamic origin:
                // var origin = request.Headers["Origin"].ToString();
                // response.Headers.AccessControlAllowOrigin = string.IsNullOrEmpty(origin) ? "*" : origin;
                response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

#if isWindowsAuth
            // Windows Authentication Support
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                var path = request.Path.ToString();
                if (path.StartsWith("/winlogin", StringComparison.CurrentCultureIgnoreCase))
                {
                    await context.ChallengeAsync(NegotiateDefaults.AuthenticationScheme);
                    return;
                }
            }
#endif

            if (!FwConfig.isTrustedHost(request.Host.ToString()))
            {
                response.StatusCode = StatusCodes.Status400BadRequest;
                await response.WriteAsync("Bad Host: request Host is not configured. Check appSettings.ROOT_DOMAIN or appSettings.override.*.hostname_match.");
                return;
            }

            // Call the FW "core" pipeline
            FW.run(context, app.Configuration);
        });

        // Run the application
        app.Run();
    }
}
