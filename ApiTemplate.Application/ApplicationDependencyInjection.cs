using ApiTemplate.Application.Interfaces;
using ApiTemplate.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ApiTemplate.Application
{
    public static class ApplicationDependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddSingleton<ISMTPService, SMTPService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IEmailTemplateService, EmailTemplateService>();
            services.AddScoped<ApplicationSignInManager, ApplicationSignInManager>();
            services.AddScoped<ApplicationUserClaimsPrincipalFactory, ApplicationUserClaimsPrincipalFactory>();

            services.AddAutoMapper(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}
