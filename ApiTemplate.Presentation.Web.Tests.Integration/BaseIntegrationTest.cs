using ApiTemplate.Infrastructure;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;
using Testcontainers.Kafka;

namespace ApiTemplate.Presentation.Web.Tests.Integration
{
    public class TestsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly MsSqlContainer _mssqlContainer = new MsSqlBuilder()
                                                             .WithCleanUp(true)
                                                             .WithImage("mcr.microsoft.com/mssql/server:2017-latest-ubuntu")
                                                             // Remove hardcoded port - let Docker assign random port to prevent conflicts
                                                             .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
                                                             .Build();

        private readonly KafkaContainer _kafkaContainer = new KafkaBuilder()
                                                             .WithImage("confluentinc/cp-kafka:7.5.0")
                                                             .WithCleanUp(true)
                                                             .Build();
        public string KafkaBootstrapServers => $"{_kafkaContainer.Hostname}:{_kafkaContainer.GetMappedPublicPort(9092)}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Override Kafka configuration with test container
                // Include all required fields to prevent validation failures
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Kafka:BootstrapServers", KafkaBootstrapServers },
                    { "Kafka:GroupId", "test-group" },
                    { "Kafka:AutoOffsetReset", "Earliest" },
                    { "Kafka:BrokerVersionFallback", "0.10.0.0" },
                    { "Kafka:ApiVersionFallbackMs", "3000" },
                    { "Kafka:SaslMechanism", "Plain" },
                    { "Kafka:SecurityProtocol", "Plaintext" },
                    { "Kafka:SaslUsername", "" },
                    { "Kafka:SaslPassword", "" },
                    { "Kafka:IsConsumptionEnabled", "true" },
                    { "Kafka:Retry:IsRetryEnabled", "true" },
                    { "Kafka:Retry:MaxRetryAttempts", "3" },
                    { "Kafka:Retry:RetryDelay10Seconds", "00:00:02" }, // 2 seconds for faster tests
                    { "Kafka:Retry:RetryDelay1Minute", "00:00:03" }, // 3 seconds for faster tests
                    { "Kafka:Retry:RetryDelay5Minutes", "00:00:05" }, // 5 seconds for faster tests
                    { "Kafka:Retry:RetryConsumerGroupId", "test-retry-group" },
                    { "Kafka:Retry:DlqConsumerGroupId", "test-dlq-group" }
                });
            });

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
            await _kafkaContainer.StartAsync();
        }

        public new async Task DisposeAsync()
        {
            await _mssqlContainer.StopAsync();
            await _kafkaContainer.StopAsync();
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