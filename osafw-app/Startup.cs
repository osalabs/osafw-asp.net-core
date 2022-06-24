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
            var db = (Hashtable)settings["db"];
            var main = (Hashtable)db["main"];
            var conn_str = (string)main["connection_string"];

            // Setup sessions server middleware
            options.ConnectionString = conn_str;
            options.SchemaName = "dbo";
            options.TableName = "fwsessions";
        });

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

        // Create branch to the MyHandlerMiddleware. 
        // All requests will follow this branch.
        app.MapWhen(context => context.Request != null,appBranch => {
            appBranch.UseMyHandler();
        });
    }
}
