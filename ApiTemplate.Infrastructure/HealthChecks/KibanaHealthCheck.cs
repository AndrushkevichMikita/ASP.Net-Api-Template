using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Http;
using System.Text.Json;

namespace ApiTemplate.Infrastructure.HealthChecks
{
    /// <summary>
    /// Health check for Kibana connectivity and status.
    /// </summary>
    public class KibanaHealthCheck : IHealthCheck
    {
        private readonly HttpClient _httpClient;
        private readonly string _kibanaUri;

        public KibanaHealthCheck(IHttpClientFactory httpClientFactory, string kibanaUri)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
            _kibanaUri = kibanaUri.TrimEnd('/');
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_kibanaUri}/api/status", cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Unhealthy(
                        $"Kibana returned status code: {response.StatusCode}",
                        data: new Dictionary<string, object> { { "statusCode", (int)response.StatusCode } });
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var statusData = JsonSerializer.Deserialize<JsonElement>(content);

                // Check if Kibana is available
                if (statusData.TryGetProperty("status", out var statusElement))
                {
                    var overallStatus = statusElement.GetProperty("overall").GetProperty("state").GetString();

                    return overallStatus switch
                    {
                        "green" => HealthCheckResult.Healthy("Kibana is healthy and ready"),
                        "yellow" => HealthCheckResult.Degraded("Kibana is available but degraded"),
                        "red" => HealthCheckResult.Unhealthy("Kibana is unavailable"),
                        _ => HealthCheckResult.Degraded($"Kibana status: {overallStatus}")
                    };
                }

                // If status structure is different, just check if we got a response
                return HealthCheckResult.Healthy("Kibana is reachable");
            }
            catch (HttpRequestException ex)
            {
                return HealthCheckResult.Unhealthy("Unable to connect to Kibana", ex);
            }
            catch (TaskCanceledException ex)
            {
                return HealthCheckResult.Unhealthy("Kibana health check timed out", ex);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Error checking Kibana health", ex);
            }
        }
    }
}


