using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Http;
using System.Text.Json;

namespace ApiTemplate.Infrastructure.HealthChecks
{
    /// <summary>
    /// Health check for Elasticsearch connectivity and cluster status.
    /// </summary>
    public class ElasticsearchHealthCheck : IHealthCheck
    {
        private readonly HttpClient _httpClient;
        private readonly string _elasticsearchUri;

        public ElasticsearchHealthCheck(IHttpClientFactory httpClientFactory, string elasticsearchUri)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
            _elasticsearchUri = elasticsearchUri.TrimEnd('/');
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_elasticsearchUri}/_cluster/health", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Unhealthy(
                        $"Elasticsearch returned status code: {response.StatusCode}",
                        data: new Dictionary<string, object> { { "statusCode", (int)response.StatusCode } });
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var healthData = JsonSerializer.Deserialize<JsonElement>(content);

                var status = healthData.GetProperty("status").GetString();

                return status switch
                {
                    "green" => HealthCheckResult.Healthy("Elasticsearch cluster is healthy (green)"),
                    // Yellow is normal for single-node clusters (no replicas), so treat as healthy
                    "yellow" => HealthCheckResult.Healthy("Elasticsearch cluster is healthy (yellow - normal for single-node)"),
                    "red" => HealthCheckResult.Unhealthy("Elasticsearch cluster is unhealthy (red)"),
                    _ => HealthCheckResult.Degraded($"Elasticsearch cluster status: {status}")
                };
            }
            catch (HttpRequestException ex)
            {
                return HealthCheckResult.Unhealthy("Unable to connect to Elasticsearch", ex);
            }
            catch (TaskCanceledException ex)
            {
                return HealthCheckResult.Unhealthy("Elasticsearch health check timed out", ex);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Error checking Elasticsearch health", ex);
            }
        }
    }
}

