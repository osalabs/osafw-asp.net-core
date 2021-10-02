using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;

namespace osafw
{
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
                //TODO MIGRATE - remove sessions_connection_string and use appSettings->db->main->connection_string AND override->XXX->db->main->connection_string
                options.ConnectionString = Startup.Configuration.GetValue<string>("sessions_connection_string");
                options.SchemaName = "dbo";
                options.TableName = "fwsessions";
            });

            // Set form limits
            services.Configure<FormOptions>(x =>
            {
                // BufferBody
                // Enables full request body buffering. Use this if multiple components need to read the raw stream. The default value is false.

                // BufferBodyLengthLimit
                // If BufferBody is enabled, this is the limit for the total number of bytes that will be buffered. Forms that exceed this limit will throw an InvalidDataException when parsed.

                // KeyLengthLimit
                // A limit on the length of individual keys. Forms containing keys that exceed this limit will throw an InvalidDataException when parsed.

                // MemoryBufferThreshold
                // If BufferBody is enabled, this many bytes of the body will be buffered in memory. If this threshold is exceeded then the buffer will be moved to a temp file on disk instead. This also applies when buffering individual multipart section bodies.

                // MultipartBodyLengthLimit
                // A limit for the length of each multipart body. Forms sections that exceed this limit will throw an InvalidDataException when parsed.

                // MultipartBoundaryLengthLimit
                // A limit for the length of the boundary identifier. Forms with boundaries that exceed this limit will throw an InvalidDataException when parsed.

                // MultipartHeadersCountLimit
                // A limit for the number of headers to allow in each multipart section. Headers with the same name will be combined. Form sections that exceed this limit will throw an InvalidDataException when parsed.

                // MultipartHeadersLengthLimit
                // A limit for the total length of the header keys and values in each multipart section. Form sections that exceed this limit will throw an InvalidDataException when parsed.

                // ValueCountLimit
                // A limit for the number of form entries to allow. Forms that exceed this limit will throw an InvalidDataException when parsed.

                // ValueLengthLimit
                // A limit on the length of individual form values. Forms containing values that exceed this limit will throw an InvalidDataException when parsed.
                x.ValueLengthLimit = 104857600; // 100Мб
                x.MultipartBodyLengthLimit = 104857600; // 100Мб
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
}
