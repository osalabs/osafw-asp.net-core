//#define isMySQL //uncomment if using MySQL, see fw/DB.cs for full instructions
#if isMySQL
using Pomelo.Extensions.Caching.MySql;
#endif

using System;
using System.Collections;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using System.Text.RegularExpressions;

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
#if isMySQL
        services.AddDistributedMySqlCache(options =>
        {
            // override settings based on env variable ASPNETCORE_ENVIRONMENT
            var enviroment = Utils.f2str(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));

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
        services.AddDistributedSqlServerCache(options =>
        {
            // override settings based on env variable ASPNETCORE_ENVIRONMENT
            var enviroment = Utils.f2str(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));

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
        });
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
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment()) {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHttpsRedirection();
            //app.UseHsts(); //enable if need Strict-Transport-Security header
        }

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

        //set stricter cookie policy
        app.UseCookiePolicy(new CookiePolicyOptions
        {
            CheckConsentNeeded = _ => false,
            HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always,
            MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
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
            //TODO FIX if set ContentType here, then responseWrite fails with "cannot write to the response body, response has completed"
            //context.Response.ContentType = "text/html; charset=utf-8"; //default content type
            //context.Response.Headers.Add("X-Content-Type-Options", "NOSNIFF"); //TODO FIX cannot set this header till fix issue with ContentType
            context.Response.Headers.Add("X-Frame-Options", "DENY"); // SAMEORIGIN allows site iframes
            context.Response.Headers.Add("X-Permitted-Cross-Domain-Policies", "master-only");
            context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
            await next();
        });

        // Create branch to the MyHandlerMiddleware.
        // All requests will follow this branch.
        app.MapWhen(context => context.Request != null,appBranch => {
            appBranch.UseMyHandler();
        });
    }
}
