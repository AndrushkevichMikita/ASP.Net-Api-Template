using ApiTemplate.Application.Services;
using ApiTemplate.Domain.Entities;
using ApiTemplate.Infrastructure;
using ApiTemplate.SharedKernel;
using ApiTemplate.SharedKernel.Extensions;
using ApiTemplate.SharedKernel.FiltersAndAttributes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Reflection;

namespace ApiTemplate.Presentation.Web
{
    public static class PresentationDependencyInjection
    {
        public const string ApiTemplateSchema = nameof(ApiTemplateSchema);
        public const string JWTWithNoExpirationSchema = nameof(JWTWithNoExpirationSchema);

        public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            services.AddControllers(config =>
            {
                config.Filters.Add(new MaxRequestSizeKBytes(Config.MaxRequestSizeBytes)); // singleton 
            }).AddJsonDefaults();

            services
            .AddRouting(options => options.LowercaseUrls = true)
            .AddHttpContextAccessor()
            .AddHttpLogging(logging =>
            {
                logging.LoggingFields = HttpLoggingFields.All;
            })
            .AddHsts(opt =>
            {
                opt.IncludeSubDomains = true;
                opt.MaxAge = TimeSpan.FromDays(365);
            })
            .AddResponseCompression(options =>
            {
                // it's risky for some attacks: https://docs.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-3.0#compression-with-secure-protocol
                options.EnableForHttps = false;
            })
            .AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "API",
                    Description = "An ASP.NET Core Web API",
                    TermsOfService = new Uri("https://example.com/terms"),
                    Contact = new OpenApiContact
                    {
                        Name = "Example Contact",
                        Url = new Uri("https://example.com/contact")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "Example License",
                        Url = new Uri("https://example.com/license")
                    }
                });
                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            })
            .AddHealthChecks();

            services.AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = ApiTemplateSchema;
                options.DefaultAuthenticateScheme = ApiTemplateSchema;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        return Task.CompletedTask;
                    }
                };

                options.TokenValidationParameters = ApplicationSignInManager.GetTokenValidationParameters(configuration);
            })
            // Initially there was an idea to use custom policy to verify jwt token integrity + skip it's expiration time,
            // but due to limitation of asp net core (you can't use [AllowAnonymous] + [Authorize(Policy="SomePolicy")]) -> https://github.com/dotnet/aspnetcore/issues/29377
            .AddJwtBearer(JWTWithNoExpirationSchema, options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        return Task.CompletedTask;
                    }
                };

                var tokenValidationParameters = ApplicationSignInManager.GetTokenValidationParameters(configuration);
                tokenValidationParameters.ValidateLifetime = false; // WARN: Since token can be already expired
                options.TokenValidationParameters = tokenValidationParameters;
            })
            .AddPolicyScheme(ApiTemplateSchema, ApiTemplateSchema, options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var jwtHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                    if (jwtHeader?.StartsWith("Bearer ") == true)
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }
                    else
                    {
                        return IdentityConstants.ApplicationScheme;
                    }
                };
            });

            services.AddDefaultIdentity<AccountEntity>(options =>
            {
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = null;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                options.Lockout.MaxFailedAccessAttempts = 6;
            }).AddRoles<IdentityRole<int>>()
              .AddEntityFrameworkStores<ApplicationDbContext>()
              .AddDefaultTokenProviders()
              .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(1);
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    return Task.CompletedTask;
                };
            });

            return services;
        }
    }
}