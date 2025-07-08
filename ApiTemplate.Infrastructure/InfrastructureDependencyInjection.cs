using ApiTemplate.Application.Interfaces;
using ApiTemplate.Domain.Entities;
using ApiTemplate.Infrastructure.Interceptors;
using ApiTemplate.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTemplate.Infrastructure
{
    public static class InfrastructureDependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());

                var connection = configuration.GetConnectionString("MSSQL") ?? throw new ArgumentNullException("Db connection");
                options.UseSqlServer(connection);
            });

            services.AddScoped(typeof(IRepository<>), typeof(TRepository<>));
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
