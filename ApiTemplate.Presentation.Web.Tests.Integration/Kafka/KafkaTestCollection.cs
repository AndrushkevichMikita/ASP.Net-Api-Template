using ApiTemplate.Presentation.Web.Tests.Integration;
using Xunit;

namespace ApiTemplate.Presentation.Web.Tests.Integration.Kafka
{
    /// <summary>
    /// Test collection for Kafka integration tests.
    /// This collection ensures tests run sequentially to avoid race conditions
    /// and port conflicts when using shared Kafka containers.
    /// 
    /// The ICollectionFixture ensures a single TestsWebApplicationFactory instance
    /// is shared across all test classes in this collection, preventing multiple
    /// containers from trying to bind to the same port.
    /// </summary>
    [CollectionDefinition("Kafka Integration Tests")]
    public class KafkaTestCollection : ICollectionFixture<TestsWebApplicationFactory>
    {
        // This class is used only for collection definition
        // The CollectionDefinition attribute ensures all tests in this collection
        // run sequentially, not in parallel
        // The ICollectionFixture ensures a single fixture instance is shared
    }
}

