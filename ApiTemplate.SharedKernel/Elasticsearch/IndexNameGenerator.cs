namespace ApiTemplate.SharedKernel.Elasticsearch
{
    /// <summary>
    /// Generates Elasticsearch index names following the pattern: logs-{serviceName}-{environment}-{version}-{purpose}
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Elasticsearch index names must be lowercase.")]
    public class IndexNameGenerator
    {
        private readonly Dictionary<string, string> _indexNames = new Dictionary<string, string>();

        /// <summary>
        /// Generates an index name based on the purpose, service name, environment, and version.
        /// </summary>
        /// <param name="purpose">The log type purpose (e.g., exceptions, webrequests, healthcheck, events)</param>
        /// <param name="serviceName">The service name (e.g., "apitemplate")</param>
        /// <param name="serviceEnvironment">The environment (e.g., "development", "production")</param>
        /// <param name="logsVersion">The logs version (e.g., "v1")</param>
        /// <returns>The generated index name in lowercase</returns>
        public string GetIndexName(string purpose, string serviceName, string serviceEnvironment, string logsVersion)
        {
            var key = $"{purpose}-{serviceName}-{serviceEnvironment}-{logsVersion}";
            
            if (_indexNames.TryGetValue(key, out string result))
            {
                return result;
            }

            // Generate index name: logs-{serviceName}-{environment}-{version}-{purpose}
            result = $"logs-{serviceName}-{serviceEnvironment}-{logsVersion}-{purpose}".ToLowerInvariant();

            _indexNames[key] = result;
            return result;
        }
    }
}
