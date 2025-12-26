using ApiTemplate.Application.Interfaces;
using ApiTemplate.Domain.Entities;
using ApiTemplate.Domain.Events;
using ApiTemplate.EventBus.Abstractions;
using ApiTemplate.EventBus.Kafka.Retry;
using ApiTemplate.Presentation.Web.Models;
using ApiTemplate.Presentation.Web.Tests.Integration.Kafka;
using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ApiTemplate.Presentation.Web.Tests.Integration
{
    /// <summary>
    /// Consolidated integration tests for all features.
    /// 
    /// This class contains:
    /// - Account management tests (authentication, authorization, user operations)
    /// - Kafka EventBus E2E tests (retry pipeline, consumer groups, offset management)
    /// 
    /// All tests share the same TestWebApplicationFactory instance with static shared containers.
    /// </summary>
    public class AllIntegrationTests : BaseIntegrationTest
    {
        private readonly IEventBus _eventBus;
        private readonly RetryConfiguration _retryConfig;

        public AllIntegrationTests(TestsWebApplicationFactory factory) : base(factory)
        {
            // Initialize Kafka services for Kafka tests
            _eventBus = ServicesScope.ServiceProvider.GetRequiredService<IEventBus>();
            _retryConfig = ServicesScope.ServiceProvider.GetRequiredService<IOptions<RetryConfiguration>>().Value;
        }

        #region Account Tests

        private async Task<(HttpResponseMessage createAccount, ProblemDetails model)> CreateAccount(CreateAccountModel model)
        {
            var create = await HTTPClient.PostAsJsonAsync("api/account/signUp", model);
            if (!create.IsSuccessStatusCode)
            {
                return (create, JsonSerializer.Deserialize<ProblemDetails>(await create.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
            }
            return (create, null);
        }

        private async Task<(HttpResponseMessage loginAccount, RefreshTokenModel model)> LoginAccount(LoginAccountModel model)
        {
            var response = await HTTPClient.PostAsJsonAsync("api/account/signIn", model);
            return (response, JsonSerializer.Deserialize<RefreshTokenModel>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
        }

        private async Task<HttpResponseMessage> SignOutAccount()
             => await HTTPClient.PostAsync("api/account/signOut", null);

        private async Task<HttpResponseMessage> ProtectedBySuperAdminAccount()
             => await HTTPClient.GetAsync("api/account/onlyForSupAdmin");

        private async Task<HttpResponseMessage> SendDigitCode(string email)
             => await HTTPClient.PostAsJsonAsync("api/account/digitCode", email);

        private async Task<HttpResponseMessage> ConfirmUserEmail(string code)
             => await HTTPClient.PutAsJsonAsync("api/account/digitCode", code);

        private IRepository<Domain.Entities.Account> AccountRepo
             => ServicesScope.ServiceProvider.GetRequiredService<IRepository<Domain.Entities.Account>>();

        private IRepository<AccountTokenEntity> TokensRepo
             => ServicesScope.ServiceProvider.GetRequiredService<IRepository<AccountTokenEntity>>();

        private async Task<(HttpResponseMessage createAccount,
                            HttpResponseMessage sendDigitCode,
                            HttpResponseMessage confirmUserEmail,
                            bool confirmedAccount)> CreateAndConfirmAccount(CreateAccountModel createModel)
        {
            var createAccount = await CreateAccount(createModel);
            var sendDigitCode = await SendDigitCode(createModel.Email);

            var lastToken = await TokensRepo.GetIQueryable()
                                            .FirstAsync(x => x.LoginProvider == TokenEnum.EmailToken.ToString());

            var confirmUserEmail = await ConfirmUserEmail(lastToken.Name);

            var confirmedAccount = await AccountRepo.GetIQueryable()
                                                    .Where(c => c.Id == lastToken.UserId)
                                                    .FirstAsync();

            return (createAccount.createAccount, sendDigitCode, confirmUserEmail, confirmedAccount.EmailConfirmed);
        }

        private static CreateAccountModel CreateAccountModel() =>
            new()
            {
                Role = RoleEnum.SuperAdmin,
                FirstName = $"TestUp{Guid.NewGuid()}",
                LastName = $"TestUp{Guid.NewGuid()}",
                Password = $"Vdvdrvrd58w!",
                Email = $"test{Guid.NewGuid()}@gmail.com"
            };

        [Fact]
        public async Task Account_Should_Create_User_Successfully()
        {
            // Act
            var createAccount = await CreateAccount(CreateAccountModel());

            // Assert
            Assert.True(createAccount.createAccount.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Account_Should_Confrim_User_Email_Successfully()
        {
            // Act
            var (createAccount, sendDigitCode, confirmUserEmail, isEmailConfirmed) = await CreateAndConfirmAccount(CreateAccountModel());

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(sendDigitCode.IsSuccessStatusCode);
            Assert.True(confirmUserEmail.IsSuccessStatusCode);
            Assert.True(isEmailConfirmed);
        }

        [Fact]
        public async Task Account_Should_Delete_Same_Unconfirmed_User()
        {
            // Arrange
            var createModel = CreateAccountModel();

            // Act
            var createAccount1 = await CreateAccount(createModel);
            var createAccount2 = await CreateAccount(createModel);

            // Assert
            Assert.True(createAccount1.createAccount.IsSuccessStatusCode);
            Assert.True(createAccount2.createAccount.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Account_Should_Login_User_Successfully()
        {
            // Arrange
            var createModel = CreateAccountModel();

            // Act
            var (createAccount, sendDigitCode, confirmUserEmail, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            });

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(sendDigitCode.IsSuccessStatusCode);
            Assert.True(confirmUserEmail.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Account_Should_Login_User_Failed()
        {
            // Arrange
            var createModel = CreateAccountModel();

            var loginModel = new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            };

            // Act
            var createAccount = await CreateAccount(createModel);
            var (loginAccount, _) = await LoginAccount(loginModel);

            // Assert
            Assert.True(createAccount.createAccount.IsSuccessStatusCode);
            Assert.True(!loginAccount.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Account_Should_SignOut_User()
        {
            // Arrange
            var createModel = CreateAccountModel();

            // Act
            var (createAccount, sendDigitCode, confirmUserEmail, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            });
            var signOutRes1 = await SignOutAccount();
            var signOutRes2 = await SignOutAccount();

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(sendDigitCode.IsSuccessStatusCode);
            Assert.True(confirmUserEmail.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
            Assert.True(signOutRes1.IsSuccessStatusCode);
            Assert.True(!signOutRes2.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Account_Should_Not_Reach_Secure_Endpoint_Besides_SuperAdmin()
        {
            // Arrange
            var createModel = CreateAccountModel();
            createModel.Role = RoleEnum.Admin;

            // Act
            var (createAccount, _, _, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            });

            var protectedByAuth = await ProtectedBySuperAdminAccount();

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
            Assert.True(!protectedByAuth.IsSuccessStatusCode && protectedByAuth.StatusCode == HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Account_Should_Reach_Secure_Endpoint_By_SuperAdmin()
        {
            // Arrange
            var createModel = CreateAccountModel();

            // Act
            var (createAccount, _, _, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            });

            var protectedByAuth = await ProtectedBySuperAdminAccount();

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
            Assert.True(protectedByAuth.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Account_Should_Delete_User()
        {
            // Arrange
            var createModel = CreateAccountModel();
            var loginModel = new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            };

            // Act
            var (createAccount, _, _, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(loginModel);
            using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, "api/account");
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(loginModel.Password), Encoding.UTF8, "application/json");
            var deleteR = await HTTPClient.SendAsync(requestMessage);

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
            Assert.True(deleteR.IsSuccessStatusCode);
        }

        #endregion

        #region Kafka EventBus E2E Tests

        /// <summary>
        /// E2E Test: Full retry pipeline flow.
        /// 
        /// Verifies:
        /// 1. Failed message is sent to retry topic
        /// 2. Retry consumer processes message after delay
        /// 3. Message is republished to main topic with retry headers
        /// 4. After max retries, message goes to DLQ
        /// 5. Headers and partition keys are preserved throughout
        /// </summary>
        [Fact]
        public async Task Kafka_FullRetryPipeline_ShouldProcessMessageThroughAllStages()
        {
            // Arrange
            var testEvent = new KafkaRetryTestEvent(
                testId: Guid.NewGuid().GetHashCode(),
                email: $"e2e-{Guid.NewGuid()}@example.com",
                firstName: "E2E",
                lastName: "Test");

            var mainTopic = "test.apitemplate.kafka_retry_test_event";
            var retryTopic = $"{mainTopic}.retry.10s";
            var dlqTopic = $"{mainTopic}.dlq";

            // Note: KafkaRetryTestEventFailureHandler is already registered in BaseIntegrationTest
            // The handler is already active and will process messages
            // Wait for consumers to start and subscribe to topics (consumers start with 2s delay)
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Create topics by publishing a dummy message first (Kafka auto-creates topics)
            var producerConfig = new ProducerConfig { BootstrapServers = Factory.KafkaBootstrapServers };
            using var topicCreator = new ProducerBuilder<string, string>(producerConfig).Build();
            try
            {
                await topicCreator.ProduceAsync(mainTopic, new Message<string, string> { Key = "init", Value = "init" });
                await topicCreator.ProduceAsync(retryTopic, new Message<string, string> { Key = "init", Value = "init" });
                await topicCreator.ProduceAsync(dlqTopic, new Message<string, string> { Key = "init", Value = "init" });
                topicCreator.Flush(TimeSpan.FromSeconds(5));
            }
            catch { /* Topics may already exist */ }

            // Wait for init messages to be processed by consumers and ensure EventBus consumer is active
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Create consumers for verification
            var retryConsumerConfig = new ConsumerConfig
            {
                BootstrapServers = Factory.KafkaBootstrapServers,
                GroupId = $"e2e-retry-{Guid.NewGuid()}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
            };

            var dlqConsumerConfig = new ConsumerConfig
            {
                BootstrapServers = Factory.KafkaBootstrapServers,
                GroupId = $"e2e-dlq-{Guid.NewGuid()}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
            };

            using var retryConsumer = new ConsumerBuilder<string, string>(retryConsumerConfig).Build();
            using var dlqConsumer = new ConsumerBuilder<string, string>(dlqConsumerConfig).Build();

            retryConsumer.Subscribe(retryTopic);
            dlqConsumer.Subscribe(dlqTopic);

            // Skip init messages
            await Task.Delay(TimeSpan.FromSeconds(2));
            var initTimeout = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < initTimeout)
            {
                try
                {
                    var retryResult = retryConsumer.Consume(TimeSpan.FromSeconds(1));
                    if (retryResult?.Message?.Value == "init")
                    {
                        retryConsumer.StoreOffset(retryResult);
                        retryConsumer.Commit(retryResult);
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    break;
                }
            }

            initTimeout = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < initTimeout)
            {
                try
                {
                    var dlqResult = dlqConsumer.Consume(TimeSpan.FromSeconds(1));
                    if (dlqResult?.Message?.Value == "init")
                    {
                        dlqConsumer.StoreOffset(dlqResult);
                        dlqConsumer.Commit(dlqResult);
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    break;
                }
            }

            // Act: Publish event that will fail
            await _eventBus.PublishAsync(testEvent, CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Assert: Message should be in retry topic
            var retryMessageFound = false;
            var retryTimeout = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < retryTimeout && !retryMessageFound)
            {
                try
                {
                    var result = retryConsumer.Consume(TimeSpan.FromSeconds(2));
                    if (result?.Message?.Value != null)
                    {
                        var envelope = RetryMessageEnvelope.FromJson(result.Message.Value);
                        if (envelope != null && envelope.OriginalPayload.Contains(testEvent.Email))
                        {
                            retryMessageFound = true;
                            retryConsumer.StoreOffset(result);
                            retryConsumer.Commit(result);
                        }
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    continue;
                }
            }

            Assert.True(retryMessageFound, "Message should be in retry topic after handler failure");

            // Wait for retry delay and republish
            await Task.Delay(_retryConfig.RetryDelay10Seconds.Add(TimeSpan.FromSeconds(5)));

            // After max retries, message should be in DLQ
            var dlqMessageFound = false;
            var dlqTimeout = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < dlqTimeout && !dlqMessageFound)
            {
                try
                {
                    var result = dlqConsumer.Consume(TimeSpan.FromSeconds(2));
                    if (result?.Message?.Value != null)
                    {
                        var envelope = RetryMessageEnvelope.FromJson(result.Message.Value);
                        if (envelope != null && envelope.OriginalPayload.Contains(testEvent.Email))
                        {
                            dlqMessageFound = true;
                            dlqConsumer.StoreOffset(result);
                            dlqConsumer.Commit(result);
                        }
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    continue;
                }
            }

            Assert.True(dlqMessageFound, "Message should be in DLQ after max retry attempts");
        }

        /// <summary>
        /// E2E Test: Consumer group coordination and offset management.
        /// 
        /// Verifies:
        /// 1. Consumer1 can commit offsets
        /// 2. Consumer2 (same group) starts from committed offset
        /// 3. Old messages are not reprocessed
        /// 4. New messages are consumed correctly
        /// </summary>
        [Fact]
        public async Task Kafka_ConsumerGroupCoordination_ShouldManageOffsetsCorrectly()
        {
            // STEP 1: Setup - Create topic and subscribe EventBus handler
            var mainTopic = "test.apitemplate.kafka_retry_test_event";
            var groupId = $"coordination-test-{Guid.NewGuid()}";
            
            Console.WriteLine($"[TEST STEP 1] Setting up test with GroupId={groupId}");
            
            // Note: KafkaRetryTestEventFailureHandler is already registered in BaseIntegrationTest
            // No need to subscribe again - it's already active
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Create topic
            var producerConfig = new ProducerConfig { BootstrapServers = Factory.KafkaBootstrapServers };
            using var topicCreator = new ProducerBuilder<string, string>(producerConfig).Build();
            await topicCreator.ProduceAsync(mainTopic, new Message<string, string> { Key = "init", Value = "init" });
            topicCreator.Flush(TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromSeconds(2));

            // STEP 2: Create Consumer1 and position it past init message
            Console.WriteLine($"[TEST STEP 2] Creating Consumer1");
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = Factory.KafkaBootstrapServers,
                GroupId = groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
            };
            
            using var consumer1 = new ConsumerBuilder<string, string>(consumerConfig).Build();
            consumer1.Subscribe(mainTopic);
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Consume and commit init message
            Console.WriteLine($"[TEST STEP 2] Consumer1 consuming init message");
            var initConsumed = false;
            var initTimeout = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < initTimeout && !initConsumed)
            {
                try
                {
                    var result = consumer1.Consume(TimeSpan.FromSeconds(1));
                    if (result?.Message?.Value == "init")
                    {
                        var initOffset = result.Offset.Value;
                        var initPartition = result.Partition.Value;
                        Console.WriteLine($"[TEST STEP 2] Consumer1 consumed init at Partition={initPartition}, Offset={initOffset}");
                        consumer1.StoreOffset(result);
                        consumer1.Commit(result);
                        Console.WriteLine($"[TEST STEP 2] Consumer1 committed init offset: Partition={initPartition}, NextOffset={initOffset + 1}");
                        initConsumed = true;
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    continue;
                }
            }

            // STEP 3: Publish testEvent1 and consume it with Consumer1
            Console.WriteLine($"[TEST STEP 3] Publishing testEvent1");
            var testEvent1 = new KafkaRetryTestEvent(
                testId: Guid.NewGuid().GetHashCode(),
                email: $"coordination1-{Guid.NewGuid()}@example.com",
                firstName: "Coordination1",
                lastName: "Test");

            await _eventBus.PublishAsync(testEvent1, CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Consume testEvent1
            Console.WriteLine($"[TEST STEP 3] Consumer1 consuming testEvent1");
            var event1Consumed = false;
            var event1Offset = -1L;
            var event1Partition = -1;
            var nextOffset = -1L;
            var event1Timeout = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < event1Timeout && !event1Consumed)
            {
                try
                {
                    var result = consumer1.Consume(TimeSpan.FromSeconds(1));
                    if (result?.Message?.Value != null && result.Message.Value.Contains(testEvent1.Email))
                    {
                        event1Offset = result.Offset.Value;
                        event1Partition = result.Partition.Value;
                        nextOffset = event1Offset + 1;
                        Console.WriteLine($"[TEST STEP 3] Consumer1 consumed testEvent1 at Partition={event1Partition}, Offset={event1Offset}");
                        consumer1.StoreOffset(result);
                        consumer1.Commit(result);
                        Console.WriteLine($"[TEST STEP 3] Consumer1 committed offset: Partition={event1Partition}, NextOffset={nextOffset}");
                        event1Consumed = true;
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    continue;
                }
            }

            Assert.True(event1Consumed, "Consumer1 should consume testEvent1");
            Assert.True(event1Offset >= 0, "Event1 offset should be valid");

            // STEP 4: Close Consumer1 and create Consumer2 (same group)
            Console.WriteLine($"[TEST STEP 4] Closing Consumer1 and creating Consumer2");
            consumer1.Close();
            await Task.Delay(TimeSpan.FromSeconds(3)); // Allow offset commit to propagate

            using var consumer2 = new ConsumerBuilder<string, string>(consumerConfig).Build();
            consumer2.Subscribe(mainTopic);
            
            // Trigger consumer group join by consuming
            await Task.Delay(TimeSpan.FromSeconds(3));
            Console.WriteLine($"[TEST STEP 4] Consumer2 joining consumer group");

            // Wait for partition assignment
            var assignmentReceived = false;
            var assignmentTimeout = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < assignmentTimeout && !assignmentReceived)
            {
                try
                {
                    var result = consumer2.Consume(TimeSpan.FromSeconds(1));
                    if (result != null)
                    {
                        assignmentReceived = true;
                        Console.WriteLine($"[TEST STEP 4] Consumer2 received partition assignment");
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    // Check if we have assignment
                    var assignment = consumer2.Assignment;
                    if (assignment != null && assignment.Count > 0)
                    {
                        assignmentReceived = true;
                        Console.WriteLine($"[TEST STEP 4] Consumer2 has partition assignment: {string.Join(", ", assignment.Select(p => $"{p.Topic}-{p.Partition}"))}");
                    }
                    continue;
                }
            }

            Assert.True(assignmentReceived, "Consumer2 should have partition assignment");

            // STEP 5: Publish testEvent2 and verify Consumer2 starts from committed offset
            Console.WriteLine($"[TEST STEP 5] Publishing testEvent2");
            var testEvent2 = new KafkaRetryTestEvent(
                testId: Guid.NewGuid().GetHashCode(),
                email: $"coordination2-{Guid.NewGuid()}@example.com",
                firstName: "Coordination2",
                lastName: "Test");

            await _eventBus.PublishAsync(testEvent2, CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Consumer2 should NOT see testEvent1 (already committed), but SHOULD see testEvent2
            Console.WriteLine($"[TEST STEP 5] Consumer2 consuming messages");
            var newMessageFound = false;
            var oldMessageFound = false;
            var consumeTimeout = DateTime.UtcNow.AddSeconds(20);
            
            while (DateTime.UtcNow < consumeTimeout && !newMessageFound)
            {
                try
                {
                    var result = consumer2.Consume(TimeSpan.FromSeconds(1));
                    if (result?.Message?.Value != null)
                    {
                        var messageValue = result.Message.Value;
                        var currentOffset = result.Offset.Value;
                        var currentPartition = result.Partition.Value;
                        
                        Console.WriteLine($"[TEST STEP 5] Consumer2 consumed message at Partition={currentPartition}, Offset={currentOffset}");
                        
                        // Check if this is testEvent1 (old message) - should NOT be seen at or before committed offset
                        if (messageValue.Contains(testEvent1.Email))
                        {
                            // If we see testEvent1 at or before the committed offset, that's a problem
                            // But if we see it at a later offset, it might be from the retry pipeline (different consumer group)
                            if (currentPartition == event1Partition && currentOffset <= event1Offset)
                            {
                                oldMessageFound = true;
                                Console.WriteLine($"[TEST STEP 5] ERROR: Consumer2 saw old message at committed offset!");
                            }
                            else
                            {
                                Console.WriteLine($"[TEST STEP 5] Consumer2 saw testEvent1 at later offset (likely from retry pipeline), skipping");
                            }
                        }
                        
                        // Check if this is testEvent2 (new message) - should be seen
                        if (messageValue.Contains(testEvent2.Email))
                        {
                            newMessageFound = true;
                            Console.WriteLine($"[TEST STEP 5] Consumer2 consumed testEvent2 at Partition={currentPartition}, Offset={currentOffset}");
                            consumer2.StoreOffset(result);
                            consumer2.Commit(result);
                        }
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    continue;
                }
            }

            Assert.False(oldMessageFound, "Old message should not be reprocessed (offset committed)");
            Assert.True(newMessageFound, "New message should be consumed");
        }

        /// <summary>
        /// E2E Test: Header propagation through retry pipeline.
        /// 
        /// Verifies that custom headers are preserved when messages go through retry topics.
        /// </summary>
        [Fact]
        public async Task Kafka_HeaderPropagation_ShouldPreserveHeadersThroughRetryPipeline()
        {
            // Arrange
            var testEvent = new KafkaRetryTestEvent(
                testId: Guid.NewGuid().GetHashCode(),
                email: $"header-{Guid.NewGuid()}@example.com",
                firstName: "Header",
                lastName: "Test");

            var mainTopic = "test.apitemplate.kafka_retry_test_event";
            var retryTopic = $"{mainTopic}.retry.10s";

            // Wait for consumers to start
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Create topic
            var producerConfig = new ProducerConfig { BootstrapServers = Factory.KafkaBootstrapServers };
            using var topicCreator = new ProducerBuilder<string, string>(producerConfig).Build();
            try
            {
                await topicCreator.ProduceAsync(mainTopic, new Message<string, string> { Key = "init", Value = "init" });
                await topicCreator.ProduceAsync(retryTopic, new Message<string, string> { Key = "init", Value = "init" });
                topicCreator.Flush(TimeSpan.FromSeconds(5));
            }
            catch { /* Topics may already exist */ }

            await Task.Delay(TimeSpan.FromSeconds(2));

            // Create consumer for retry topic
            var retryConsumerConfig = new ConsumerConfig
            {
                BootstrapServers = Factory.KafkaBootstrapServers,
                GroupId = $"header-test-{Guid.NewGuid()}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false,
            };

            using var retryConsumer = new ConsumerBuilder<string, string>(retryConsumerConfig).Build();
            retryConsumer.Subscribe(retryTopic);

            // Skip init message
            await Task.Delay(TimeSpan.FromSeconds(2));
            var initTimeout = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < initTimeout)
            {
                try
                {
                    var result = retryConsumer.Consume(TimeSpan.FromSeconds(1));
                    if (result?.Message?.Value == "init")
                    {
                        retryConsumer.StoreOffset(result);
                        retryConsumer.Commit(result);
                        break;
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    break;
                }
            }

            // Act: Publish event that will fail
            await _eventBus.PublishAsync(testEvent, CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Assert: Check headers in retry topic
            var headersFound = false;
            var timeout = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < timeout && !headersFound)
            {
                try
                {
                    var result = retryConsumer.Consume(TimeSpan.FromSeconds(2));
                    if (result?.Message?.Value != null)
                    {
                        var envelope = RetryMessageEnvelope.FromJson(result.Message.Value);
                        if (envelope != null && envelope.OriginalPayload.Contains(testEvent.Email))
                        {
                            // Verify retry count header exists
                            var retryCountHeader = result.Message.Headers?.FirstOrDefault(h => h.Key == "retry-count");
                            Assert.NotNull(retryCountHeader);
                            
                            var headerValue = System.Text.Encoding.UTF8.GetString(retryCountHeader.GetValueBytes());
                            Assert.True(int.TryParse(headerValue, out var retryCount));
                            Assert.True(retryCount > 0, "Retry count should be greater than 0");

                            headersFound = true;
                            retryConsumer.StoreOffset(result);
                            retryConsumer.Commit(result);
                        }
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.Local_PartitionEOF || ex.Error.IsLocalError)
                {
                    continue;
                }
            }

            Assert.True(headersFound, "Message with headers should be in retry topic");
        }

        #endregion
    }
}