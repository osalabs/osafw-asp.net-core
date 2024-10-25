//#define isMySQL //uncomment if using MySQL, see fw/DB.cs for full instructions
#if isMySQL
using Pomelo.Extensions.Caching.MySql;
#endif

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections;
using System.Security.Claims;
using System.Threading.Tasks;

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
        services.AddDistributedSqlServerCache((Action<Microsoft.Extensions.Caching.SqlServer.SqlServerCacheOptions>)(options =>
        {
            // override settings based on env variable ASPNETCORE_ENVIRONMENT
            var enviroment = Utils.toStr((object)Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));

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
        services.AddHttpContextAccessor();

        services.AddTransient<IFW, FW>(s =>
        {
            var httpContextAccessor = s.GetRequiredService<IHttpContextAccessor>();
            var configuration = s.GetRequiredService<IConfiguration>();

            return new FW(httpContextAccessor.HttpContext, configuration);
        });

        var authBuilder = services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddCookie();

        // External auth configuration.

        var enable_external_login = false;

        var googleConfigSectionName = $"appSettings:{Constants.ExtLogin.GoogleSettingsKey}";
        var google_settings = new ExtAuthSettings();
        Configuration.GetSection(googleConfigSectionName).Bind(google_settings);

        if (google_settings.Enabled)
        {
            enable_external_login = true;

            authBuilder.AddGoogle(options =>
            {
                options.ClientId = google_settings.ClientId;
                options.ClientSecret = google_settings.ClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.Events.OnTicketReceived = AuthOnTicketReceived;
            });
        }

        var microsoftConfigSectionName = $"appSettings:{Constants.ExtLogin.MicrosoftSettingsKey}";
        var microsoft_settings = new ExtAuthSettings();
        Configuration.GetSection(microsoftConfigSectionName).Bind(microsoft_settings);

        if (microsoft_settings.Enabled)
        {
            enable_external_login = true;

            authBuilder.AddMicrosoftAccount(options =>
            {
                options.ClientId = microsoft_settings.ClientId;
                options.ClientSecret = microsoft_settings.ClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.Events.OnTicketReceived = AuthOnTicketReceived;
            });
        }

        if (enable_external_login)
        {
            authBuilder.AddCookie(IdentityConstants.ExternalScheme);
        }

        services.AddControllers();
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
            // MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Strict, // with Strict setting, external links to app won't keep session
            MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.Lax, // Lax - external links to app will keep session
            Secure = CookieSecurePolicy.SameAsRequest,
            OnAppendCookie = (context) =>
            {
                context.IssueCookie = true;
            },
            OnDeleteCookie = (context) =>
            {
            }
        });

        app.Map("/mvc", builder =>
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        });

        app.UseAuthentication();

        // security headers
        app.Use(async (context, next) =>
        {
            //TODO FIX if set ContentType here, then responseWrite fails with "cannot write to the response body, response has completed"
            //context.Response.ContentType = "text/html; charset=utf-8"; //default content type
            //context.Response.Headers.Add("X-Content-Type-Options", "NOSNIFF"); //TODO FIX cannot set this header till fix issue with ContentType
            context.Response.Headers.Append("X-Frame-Options", "DENY"); // SAMEORIGIN allows site iframes
            context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "master-only");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            await next();
        });

        // Create branch to the MyHandlerMiddleware.
        // All requests will follow this branch.
        app.MapWhen(
            context => context.Request != null
                && context.Request.Path.Value?.Contains("mvc/", StringComparison.InvariantCultureIgnoreCase) != true,
            appBranch =>
            {
                appBranch.UseMyHandler();
            });
    }

    private Task AuthOnTicketReceived(TicketReceivedContext context)
    {
        if (context.Principal.Identity.IsAuthenticated)
        {
            var serviceProvider = context.HttpContext.RequestServices;
            using var fw = serviceProvider.GetService<IFW>();

            var user = fw.model<Users>().oneByEmail(context.Principal.FindFirstValue(ClaimTypes.Email));

            if (user.Count > 0 && Utils.f2int(user["status"]) == Users.STATUS_ACTIVE)
            {
                fw.model<Users>().doLogin(Utils.f2int(user["id"]));
            }

            context.Success();
        }

        return Task.CompletedTask;
    }
}