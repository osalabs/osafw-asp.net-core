//#define isMySQL // uncomment if using MySQL, see fw/DB.cs for full instructions
#if isMySQL
#endif

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
using System;
using System.Collections;

namespace osafw;

public static class Program
{
    public static void Main(string[] args)
    {
        // Install Playwright full headless Chromium build on first run.
        // The installer will detect existing binaries and skip re-downloading,
        // making this safe to run every time the application starts.
        // Additionally, if you update or downgrade the Microsoft.Playwright package,
        // the installer will remove any out-of-sync Chromium versions and
        // install the browser binary matching the library version,
        // ensuring compatibility between Playwright and the browser.
        Microsoft.Playwright.Program.Main(
        [
            "install",          // invoke Playwright installer
            "chromium",         // install the full Chromium build
            "--with-deps",      // include OS-level dependencies
            "--no-shell"        // skip the legacy headless shell
        ]);

        // In .NET 6+ the recommended pattern is the "WebApplication.CreateBuilder" approach
        var builder = WebApplication.CreateBuilder(args);

        // If you use Sentry, enable it here:
        // builder.WebHost.UseSentry();

        // read the environment settings
        var settings = FwConfig.settingsForEnvironment(builder.Configuration);
        var isDevelopmentEnv = settings["IS_DEV"].toBool();

        // Retrieve main DB connection info
        var dbSection = (Hashtable)settings["db"];
        var mainDB = (Hashtable)dbSection["main"];
        var connStr = (string)mainDB["connection_string"];
        var dbType = (string)mainDB["type"];

        // Site name used in data-protection app name
        var appName = (string)(settings["SITE_NAME"] ?? "osafw");

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
        builder.Services.AddDataProtection().SetApplicationName(appName);
        builder.Services.Configure<KeyManagementOptions>(options =>
            {
                options.XmlRepository = repository; // i.e. "PersistKeysToCustomXmlRepository"
            });

#if isMySQL
        // If using MySQL for distributed cache (and sessions)
        builder.Services.AddDistributedMySqlCache(options =>
        {
            var csb = new MySqlConnector.MySqlConnectionStringBuilder(connStr);
            if (string.IsNullOrEmpty(csb.Database))
                throw new ApplicationException("No database name defined in connection_string");

            // Setup session store
            options.ConnectionString = csb.ConnectionString;
            options.SchemaName = csb.Database; // database name
            options.TableName = "fwsessions";
        });
#else
        // If using SQL Server for distributed cache (and sessions)
        builder.Services.AddDistributedSqlServerCache(options =>
        {
            options.ConnectionString = connStr;
            options.SchemaName = "dbo";
            options.TableName = "fwsessions";
        });
#endif

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

        // Windows Active Directory authentication support (optional)
        // builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();

        // Memory cache
        builder.Services.AddMemoryCache();

        // register FW core services
        builder.Services.AddFwServices(builder.Configuration);

        // Uncomment to enable scheduled tasks
        // builder.Services.AddHostedService<FwCronService>();

        // Build the WebApplication
        var app = builder.Build();

        FW.service_provider = app.Services;

        //-------------------------------
        // Configure the middleware pipeline
        //-------------------------------

        FwCache.MemoryCache = app.Services.GetRequiredService<IMemoryCache>();

        // If dev - developer exception page
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
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

        // Finally, map all requests to FW-based handler:
        app.MapWhen(ctx => ctx.Request != null, appBranch =>
        {
            appBranch.UseMyHandler(); // calls the MyHandlerMiddleware (see HttpMiddleware.cs)
        });

        // Run the application
        app.Run();
    }
}