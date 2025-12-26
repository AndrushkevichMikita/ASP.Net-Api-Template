using ApiTemplate.Application.Interfaces;
using ApiTemplate.Infrastructure;
using ApiTemplate.EventBus.Abstractions;
using ApiTemplate.EventBus.Kafka;
using ApiTemplate.EventBus.Kafka.Retry;
using ApiTemplate.Presentation.Web.Tests.Integration.Kafka;
using Confluent.Kafka;
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
                
                // Use a factory to get connection string lazily (after containers are initialized)
                // Use a factory to lazily get the connection string after containers are initialized
                services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
                {
                    // Wait for container to be initialized (with timeout)
                    var timeout = DateTime.UtcNow.AddSeconds(60);
                    while (_sharedMssqlContainer == null && DateTime.UtcNow < timeout)
                    {
                        Thread.Sleep(100);
                    }
                    
                    if (_sharedMssqlContainer == null)
                    {
                        throw new InvalidOperationException("MSSQL container not initialized within timeout. Check container startup.");
                    }
                    
                    var c = _sharedMssqlContainer.GetConnectionString();
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
            bool shouldInitialize = false;
            lock (_containerLock)
            {
                if (!_containersInitialized)
                {
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
                    shouldInitialize = true;
                }
            }
            
            // Only start containers if we created them (outside the lock to avoid deadlocks)
            if (shouldInitialize)
            {
                await _sharedMssqlContainer.StartAsync();
                await _sharedKafkaContainer.StartAsync();
                
                // Additional health check: Verify Kafka broker is ready by executing health check command
                // This ensures Kafka is fully initialized before tests start
                await WaitForKafkaReadyAsync();
            }
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
            bool shouldDispose = false;
            lock (_containerLock)
            {
                if (!_containersInitialized)
                {
                    return;
                }
                _containersInitialized = false;
                shouldDispose = true;
            }
            
            if (!shouldDispose)
            {
                return;
            }
            
            // CRITICAL: Stop all EventBus consumers BEFORE disposing Kafka container
            // This prevents AccessViolationException when consumers try to consume from disposed broker
            // Note: We only stop consumers manually - singletons (AdminClient, Producer) are disposed by DI container
            try
            {
                // Get services from the factory's service provider
                // Use Server.Services if available, otherwise fall back to Services property
                IServiceProvider? serviceProvider = null;
                try
                {
                    serviceProvider = Server?.Services ?? Services;
                }
                catch
                {
                    // If Server is not available or disposed, try Services directly
                    try
                    {
                        serviceProvider = Services;
                    }
                    catch
                    {
                        // If both fail, we can't access services - log and continue
                        System.Diagnostics.Debug.WriteLine("Cannot access service provider for EventBus disposal");
                    }
                }
                
                if (serviceProvider != null)
                {
                    // Access services directly from the service provider
                    // Note: We only stop consumers manually - singletons are disposed by DI container
                    using var scope = serviceProvider.CreateScope();
                    var scopedProvider = scope.ServiceProvider;
                    
                    // Stop EventBus (KafkaClient) - this stops all consumers
                    try
                    {
                        if (scopedProvider.GetService<IEventBus>() is IDisposable eventBus)
                        {
                            eventBus.Dispose();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed - ignore
                    }
                    
                    // Stop RetryPipelineService - this stops retry consumers
                    try
                    {
                        if (scopedProvider.GetService<IRetryPipelineService>() is IDisposable retryPipelineService)
                        {
                            retryPipelineService.Dispose();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed - ignore
                    }
                    
                    // Stop RetryProducer - this stops retry producers
                    try
                    {
                        if (scopedProvider.GetService<IRetryProducer>() is IDisposable retryProducer)
                        {
                            retryProducer.Dispose();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed - ignore
                    }
                    
                    // Stop SubscriptionsProcessor - this stops subscription processing
                    try
                    {
                        if (scopedProvider.GetService<ISubscriptionsProcessor>() is IDisposable subscriptionsProcessor)
                        {
                            subscriptionsProcessor.Dispose();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Already disposed - ignore
                    }
                    
                    // Flush main Kafka producer (singleton) - let DI container handle disposal
                    // Note: We don't manually dispose singletons - the DI container will handle it
                    // But we flush to ensure pending messages are sent before shutdown
                    try
                    {
                        var mainProducer = scopedProvider.GetService<IProducer<string, string>>();
                        if (mainProducer != null)
                        {
                            try
                            {
                                mainProducer.Flush(TimeSpan.FromSeconds(5));
                            }
                            catch (ObjectDisposedException)
                            {
                                // Producer already disposed - ignore
                            }
                            catch
                            {
                                // Ignore other flush errors during shutdown
                            }
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Producer already disposed - ignore
                    }
                    catch
                    {
                        // Ignore any other errors during producer access
                    }
                    
                    // Give consumers time to stop gracefully
                    // Increased delay to allow background tasks to complete
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    
                    // Scope will be disposed here automatically
                    // Note: AdminClient and Producer are singletons managed by DI container
                    // They will be disposed when the host is disposed, which happens after containers
                    // 
                    // KNOWN ISSUE: AdminClient may throw ObjectDisposedException during host disposal
                    // This is a Confluent.Kafka issue where AdminClient tries to cancel a CancellationTokenSource
                    // that's already been disposed. This does not affect test functionality - all tests pass.
                    // The exception occurs during test class cleanup after tests complete.
                    // See: KAFKA_EVENTBUS_ARCHITECTURE.md for details
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - we still need to dispose containers
                // ObjectDisposedException from AdminClient during DI container disposal is expected
                System.Diagnostics.Debug.WriteLine($"Error during EventBus services shutdown: {ex.Message}");
                // Still wait a bit even if shutdown failed, to allow any in-flight operations to complete
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            
            // Now safe to dispose containers
            // Stop containers first, then dispose to ensure ports are released
            if (_sharedKafkaContainer != null)
            {
                try
                {
                    await _sharedKafkaContainer.StopAsync();
                    await Task.Delay(TimeSpan.FromSeconds(1)); // Give port time to release
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping Kafka container: {ex.Message}");
                }
                
                try
                {
                    await _sharedKafkaContainer.DisposeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing Kafka container: {ex.Message}");
                }
            }
            
            if (_sharedMssqlContainer != null)
            {
                try
                {
                    await _sharedMssqlContainer.StopAsync();
                    await Task.Delay(TimeSpan.FromSeconds(1)); // Give port time to release
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping MSSQL container: {ex.Message}");
                }
                
                try
                {
                    await _sharedMssqlContainer.DisposeAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing MSSQL container: {ex.Message}");
                }
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
                    var eventBus = factory.Services.GetRequiredService<EventBus.Abstractions.IEventBus>();
                    eventBus.Subscribe<KafkaTestEvent, KafkaTestEventFailureHandler>(numberOfConsumers: 1);
                    eventBus.Subscribe<KafkaRetryTestEvent, KafkaRetryTestEventFailureHandler>(numberOfConsumers: 1);
                    _testSubscriptionsRegistered = true;
                }
            }
        }
    }
}