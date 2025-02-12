using ApiTemplate.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Testcontainers.MsSql;

namespace ApiTemplate.Presentation.Web.Tests.Integration
{
    public class SqlServerWaitStrategy : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            var timeout = TimeSpan.FromMinutes(2);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    var result = await container.ExecAsync(new[] { "/opt/mssql-tools/bin/sqlcmd", "-Q", "SELECT 1;" });

                    if (result.ExitCode == 0)
                    {
                        return true; // ✅ SQL Server is ready
                    }
                }
                catch
                {
                    // Ignore errors and retry
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            return false; // ❌ Timed out
        }
    }

    public class TestsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly MsSqlContainer _mssqlContainer = new MsSqlBuilder()
                                                             .WithCleanUp(true)
                                                             .WithImage("mcr.microsoft.com/mssql/server:2017-latest-ubuntu")
                                                             .WithPortBinding(1433, true) // Bind a random available port
                                                             .WithWaitStrategy(
                                                                     Wait.ForUnixContainer()
                                                                    .AddCustomWaitStrategy(new SqlServerWaitStrategy()))
                                                             .Build();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                var dbContextDescriptor = services.SingleOrDefault(x => x.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (dbContextDescriptor is not null)
                {
                    services.Remove(dbContextDescriptor);
                }
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    var c = _mssqlContainer.GetConnectionString();
                    options.UseSqlServer(c);
                });
            });

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");
            builder.UseEnvironment("IntegrationTests");
        }

        public async Task InitializeAsync()
        {
            await _mssqlContainer.StartAsync();
        }

        public new async Task DisposeAsync()
        {
            await _mssqlContainer.StopAsync();
        }
    }

    /// <summary>
    /// Note: <see cref="TestsWebApplicationFactory" /> class are tied to IAsyncLifetime, <br/>
    /// then the setup and teardown will occur only once per test class, not per [Fact] method
    /// </summary>
    public abstract class BaseIntegrationTest : IClassFixture<TestsWebApplicationFactory>
    {
        public readonly IServiceScope ServicesScope;
        public readonly TestsWebApplicationFactory Factory;
        public readonly HttpClient HTTPClient;

        protected BaseIntegrationTest(TestsWebApplicationFactory factory)
        {
            Factory = factory;
            ServicesScope = factory.Services.CreateScope();
            HTTPClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });
        }
    }
}
