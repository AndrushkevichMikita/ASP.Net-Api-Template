using ApiTemplate.Application.Interfaces;
using ApiTemplate.Infrastructure;
using ApiTemplate.Infrastructure.EventBus;
using ApiTemplate.Presentation.Web.Tests.Integration.Kafka;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Testcontainers.Kafka;

namespace ApiTemplate.Presentation.Web.Tests.Integration
{
    public class TestsWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private static readonly object _containerLock = new object();
        private static MsSqlContainer _sharedMssqlContainer;
        private static KafkaContainer _sharedKafkaContainer;
        private static bool _containersInitialized = false;
        
        private MsSqlContainer _mssqlContainer => _sharedMssqlContainer;
        private KafkaContainer _kafkaContainer => _sharedKafkaContainer;
        
        // Use fixed port 9094 for test Kafka (dev uses 9092, 9093)
        public string KafkaBootstrapServers => "localhost:9094";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                // Override Kafka configuration with test container
                // Use port 9094 (different from local dev Kafka on 9092, 9093) and unique consumer groups
                var testRunId = Guid.NewGuid().ToString("N")[..8];
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "Kafka:BootstrapServers", KafkaBootstrapServers },
                    { "Kafka:GroupId", $"test-group-{testRunId}" },
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
                    { "Kafka:Retry:RetryConsumerGroupId", $"test-retry-group-{testRunId}" },
                    { "Kafka:Retry:DlqConsumerGroupId", $"test-dlq-group-{testRunId}" }
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

                // Register test-specific event handlers (instead of production handlers)
                services.AddScoped<KafkaTestEventFailureHandler>();
                services.AddScoped<KafkaRetryTestEventFailureHandler>();
            });

            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "IntegrationTests");
            builder.UseEnvironment("IntegrationTests");
        }

        public async Task InitializeAsync()
        {
            // Only initialize containers once, even if multiple factory instances are created
            lock (_containerLock)
            {
                if (_containersInitialized)
                {
                    // Containers already initialized by another factory instance
                    return;
                }
                
                // Create shared containers (only once)
                _sharedMssqlContainer = new MsSqlBuilder()
                    .WithCleanUp(true)
                    .WithImage("mcr.microsoft.com/mssql/server:2017-latest-ubuntu")
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1433))
                    .Build();

                _sharedKafkaContainer = new KafkaBuilder()
                    .WithImage("confluentinc/cp-kafka:7.5.0")
                    .WithCleanUp(true)
                    .WithPortBinding(9094, 9092) // Host port 9094, container port 9092
                    .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", "PLAINTEXT://localhost:9094")
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(9092))
                    .Build();
                
                _containersInitialized = true;
            }
            
            await _mssqlContainer.StartAsync();
            await _kafkaContainer.StartAsync();
            
            // Additional health check: Verify Kafka broker is ready by executing health check command
            // This ensures Kafka is fully initialized before tests start
            await WaitForKafkaReadyAsync();
        }

        private async Task WaitForKafkaReadyAsync()
        {
            const int maxRetries = 30;
            const int delayMs = 1000;
            
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Execute health check command inside container to verify Kafka is ready
                    var execResult = await _kafkaContainer.ExecAsync(new[]
                    {
                        "kafka-broker-api-versions",
                        "--bootstrap-server",
                        "localhost:9092"
                    });
                    
                    if (execResult.ExitCode == 0)
                    {
                        // Kafka is ready
                        return;
                    }
                }
                catch
                {
                    // Command failed, Kafka might not be ready yet - continue retrying
                }
                
                // Wait before retrying
                await Task.Delay(delayMs);
            }
            
            // If we get here, Kafka didn't pass the health check in time
            // However, the port wait strategy should have ensured the container is at least partially ready
            // We continue anyway - tests will fail naturally if Kafka isn't actually ready
            // This is more lenient than throwing an exception which would prevent all tests from running
        }

        public new async Task DisposeAsync()
        {
            // Only dispose containers once, even if multiple factory instances exist
            lock (_containerLock)
            {
                if (!_containersInitialized)
                {
                    return;
                }
                _containersInitialized = false;
            }
            
            if (_sharedMssqlContainer != null)
            {
                await _sharedMssqlContainer.StopAsync();
                await _sharedMssqlContainer.DisposeAsync();
            }
            
            if (_sharedKafkaContainer != null)
            {
                await _sharedKafkaContainer.StopAsync();
                await _sharedKafkaContainer.DisposeAsync();
            }
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
        private static bool _testSubscriptionsRegistered = false;
        private static readonly object _subscriptionLock = new object();

        protected BaseIntegrationTest(TestsWebApplicationFactory factory)
        {
            Factory = factory;
            ServicesScope = factory.Services.CreateScope();
            HTTPClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });

            // Register test event subscriptions (only once, thread-safe)
            lock (_subscriptionLock)
            {
                if (!_testSubscriptionsRegistered)
                {
                    var eventBus = factory.Services.GetRequiredService<IEventBus>();
                    eventBus.Subscribe<KafkaTestEvent, KafkaTestEventFailureHandler>(numberOfConsumers: 1);
                    eventBus.Subscribe<KafkaRetryTestEvent, KafkaRetryTestEventFailureHandler>(numberOfConsumers: 1);
                    _testSubscriptionsRegistered = true;
                }
            }
        }
    }
}