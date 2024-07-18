﻿using ApiTemplate.Application.Interfaces;
using ApiTemplate.Application.Services;
using ApiTemplate.Domain.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Reflection;

namespace ApiTemplate.Application
{
    public static class ApplicationDependencyInjection
    {
        public const string JWTWithNoExpirationSchema = nameof(JWTWithNoExpirationSchema);

        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration, string schema)
        {
            services.AddSingleton<ISMTPService, SMTPService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IEmailTemplateService, EmailTemplateService>();
            services.AddScoped<ApplicationSignInManager, ApplicationSignInManager>();
            services.AddScoped<ApplicationUserClaimsPrincipalFactory, ApplicationUserClaimsPrincipalFactory>();

            services.AddAutoMapper(Assembly.GetExecutingAssembly());

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

            services.AddAuthentication(options =>
            {
                options.DefaultChallengeScheme = "smart";
                options.DefaultAuthenticateScheme = "smart";
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
            .AddPolicyScheme("smart", "Smart Authentication", options =>
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
                        return schema;
                    }
                };
            });

            return services;
        }
    }
}
