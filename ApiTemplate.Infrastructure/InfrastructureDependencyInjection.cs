using ApiTemplate.Domain.Entities;
using ApiTemplate.Domain.Interfaces;
using ApiTemplate.Infrastructure.Interceptors;
using ApiTemplate.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace ApiTemplate.Infrastructure
{
    public static class InfrastructureDependencyInjection
    {
        public static IServiceCollection AddInfrastructure<TFactory>(this IServiceCollection services, IConfiguration configuration)
            where TFactory : UserClaimsPrincipalFactory<AccountEntity, IdentityRole<int>>
        {
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

                var connection = configuration.GetConnectionString("MSSQL") ?? throw new ArgumentNullException("Db connection");
                options.UseSqlServer(connection);
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
              .AddClaimsPrincipalFactory<TFactory>();

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

            services.AddScoped(typeof(IRepo<>), typeof(TRepository<>));
            services.AddScoped<ISaveChangesInterceptor, AccountEntityInterceptor>();
#if DEBUG
            services.AddDatabaseDeveloperPageExceptionFilter();
#endif
            return services;
        }

        public static async Task ApplyDbMigrations(this IServiceProvider servicesProvider, IConfiguration configuration)
        {
            var scope = servicesProvider.CreateScope().ServiceProvider;

            var db = scope.GetRequiredService<ApplicationDbContext>().Database;
            if ((await db.GetPendingMigrationsAsync()).Any())
                await db.MigrateAsync();

            // adding roles
            var roleManager = scope.GetRequiredService<RoleManager<IdentityRole<int>>>();
            foreach (var role in Enum.GetNames(typeof(RoleEnum)))
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole<int> { Name = role });
        }
    }

    // to fix running startup during migrations-update
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            //builder.Configuration.ApplyConfiguration();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            options.UseSqlServer(builder.Configuration.GetConnectionString("MSSQL"));
            return new ApplicationDbContext(options.Options);
        }
    }
}
