//#define isMySQL //uncomment if using MySQL, see fw/DB.cs for full instructions
#if isMySQL
using Pomelo.Extensions.Caching.MySql;
#endif

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
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
using System.IO;

namespace osafw;

public class Startup
{
    public static IConfiguration Configuration { get; set; }

    public Startup(IConfiguration configuration)
    {
        Startup.Configuration = configuration;
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        // this will save key to file system, so it's shared between multiple instances and not reset on app restart
        services.AddDataProtection()
        .SetApplicationName("osafw")
        .PersistKeysToFileSystem(new DirectoryInfo(Utils.getTmpDir()));

        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.MimeTypes = ["text/plain", "text/html", "text/css", "application/javascript", "text/javascript", "text/xml", "text/csv", "application/json", "image/svg+xml"];
        });

#if isMySQL
        services.AddDistributedMySqlCache(options =>
        {
            // override settings based on env variable ASPNETCORE_ENVIRONMENT
            var enviroment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT").toStr();

            var appSessings = new Hashtable();
            FwConfig.readSettingsSection(Startup.Configuration.GetSection("appSettings"), ref appSessings);

            // Try override settings by name
            var settings = (Hashtable)appSessings["appSettings"];
            FwConfig.overrideSettingsByName(enviroment, ref settings);

            // Retriving db connection string
            var db = (Hashtable)settings["db"];
            var main = (Hashtable)db["main"];
            var conn_str = (string)main["connection_string"]; //MySQL connection string ex: "Server=127.0.0.1;User ID=root;Password=;Database=demo;Allow User Variables=true;"

            //extract
            var m = Regex.Match(conn_str, @"Database=(\w+)", RegexOptions.IgnoreCase);
            if (!m.Success)
                throw new ApplicationException("No database name defined in connection_string");

            // Setup sessions server middleware
            options.ConnectionString = conn_str;
            options.SchemaName = m.Groups[1].Value; //database name
            options.TableName = "fwsessions";
        });
#endif
#if !isMySQL
        services.AddDistributedSqlServerCache((Action<Microsoft.Extensions.Caching.SqlServer.SqlServerCacheOptions>)(options =>
        {
            // override settings based on env variable ASPNETCORE_ENVIRONMENT
            var enviroment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT").toStr();

            var appSessings = new Hashtable();
            FwConfig.readSettingsSection(Startup.Configuration.GetSection("appSettings"), ref appSessings);

            // Try override settings by name
            var settings = (Hashtable)appSessings["appSettings"];
            FwConfig.overrideSettingsByName(enviroment, ref settings);

            // Retriving db connection string
            // TrustServerCertificate=true; should be present if using Microsoft.Extensions.Caching.SqlServer v7 or above
            // or use Encrypt=False;
            var db = (Hashtable)settings["db"];
            var main = (Hashtable)db["main"];
            var conn_str = (string)main["connection_string"];

            // Setup sessions server middleware
            options.ConnectionString = conn_str;
            options.SchemaName = "dbo";
            options.TableName = "fwsessions";
        }));
#endif
        // Set form limits
        services.Configure<FormOptions>(options =>
        {
            //options.ValueLengthLimit = 104857600; // 100MB, default is 4MB - form values
            //options.MultipartBodyLengthLimit = 1073741824; // 1GB, ddefault is 128MB - for file uploads
        });

        services.Configure<IISServerOptions>(options =>
        {
            options.AllowSynchronousIO = false;
        });

        services.AddSession(options =>
        {
            options.Cookie.IsEssential = true;
            if ((int)Startup.Configuration.GetValue(typeof(int), "sessionIdleTimeout") > 0)
            {
                options.IdleTimeout = TimeSpan.FromSeconds((int)Startup.Configuration.GetValue(typeof(int), "sessionIdleTimeout"));
            }
            if (Startup.Configuration.GetValue(typeof(bool), "cookieHttpOnly") != null)
            {
                options.Cookie.HttpOnly = (bool)Startup.Configuration.GetValue(typeof(bool), "cookieHttpOnly");
            }
        });

        // Windows Active Directory authentication support
        // https://learn.microsoft.com/en-us/aspnet/core/security/authentication/windowsauth?view=aspnetcore-7.0&tabs=visual-studio#iisiis-express
        // first, install package Microsoft.AspNetCore.Authentication.Negotiate
        // services.AddAuthentication(Microsoft.AspNetCore.Authentication.Negotiate.NegotiateDefaults.AuthenticationScheme).AddNegotiate();

        services.AddMemoryCache();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache cache)
    {
        FwCache.MemoryCache = cache;

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHttpsRedirection();
            //app.UseHsts(); //enable if need Strict-Transport-Security header
        }

        app.UseResponseCompression();

        //enable aggressive caching of static files
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = (context) =>
            {
                var headers = context.Context.Response.GetTypedHeaders();
                headers.CacheControl = new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromDays(365)
                };
            }
        });
        app.UseSession();

        //set cookie policy
        app.UseCookiePolicy(new CookiePolicyOptions
        {
            CheckConsentNeeded = _ => false,
            HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always,
            MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Strict, // with Strict setting, external links to app won't keep session
            //MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Lax, // Lax - external links to app will keep session
            Secure = CookieSecurePolicy.SameAsRequest,
            OnAppendCookie = (context) =>
            {
                context.IssueCookie = true;
            },
            OnDeleteCookie = (context) =>
            {
            }
        });

        // security headers
        app.Use(async (context, next) =>
        {
            context.Response.ContentType = "text/html; charset=utf-8"; //default content type
            context.Response.Headers.XContentTypeOptions = "NOSNIFF";
            context.Response.Headers.XFrameOptions = "DENY"; // SAMEORIGIN allows site iframes
            context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "master-only");
            context.Response.Headers.XXSSProtection = "1; mode=block";
            await next();
        });

        // Create branch to the MyHandlerMiddleware.
        // All requests will follow this branch.
        app.MapWhen(context => context.Request != null, appBranch =>
        {
            appBranch.UseMyHandler();
        });
    }
}
