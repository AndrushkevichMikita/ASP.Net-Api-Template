using Serilog.Events;

namespace ApiTemplate.SharedKernel.Elasticsearch
{
    /// <summary>
    /// Decides which Elasticsearch index to use based on log event properties.
    /// Routes logs to different indexes based on SourceContext, log level, and message content.
    /// </summary>
    public class ElasticsearchIndexDecider
    {
        private const string SourceContext = "SourceContext";
        private const string UriName = "Uri";
        private const string RequestPathName = "RequestPath";
        private const string RequestBody = "{RequestBody}";
        private const string ResponseBody = "{ResponseBody}";

        private const string HealthChecksNamespacePrefix = "HealthChecks";
        private const string IHealthCheckNamespace = "Microsoft.Extensions.Diagnostics.HealthChecks";
        private const string ExceptionHandlerMiddlewareName = "Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware";
        private const string HttpLoggingMiddlewareName = "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware";
        
        // HTTP request properties to detect web request logs
        private const string HttpMethodName = "Method";
        private const string HttpStatusCodeName = "StatusCode";
        private const string HttpPathName = "Path";

        private static readonly List<string> HealthCheckEvents = new List<string>
        {
            "healthcheck",
            "healthz",
        };

        private readonly IndexNameGenerator _indexNameGenerator = new IndexNameGenerator();
        private readonly string _serviceName;
        private readonly string _serviceEnvironment;
        private readonly string _logsVersion;

        public ElasticsearchIndexDecider(string serviceName, string serviceEnvironment, string logsVersion)
        {
            _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            _serviceEnvironment = serviceEnvironment ?? throw new ArgumentNullException(nameof(serviceEnvironment));
            _logsVersion = logsVersion ?? throw new ArgumentNullException(nameof(logsVersion));
        }

        private enum Purpose
        {
            Healthcheck,
            Exceptions,
            Events,
            Methodparameters,
            Webbodies,
            Webrequests,
        }

        /// <summary>
        /// Decides which index to use for the given log event.
        /// </summary>
        /// <param name="logEvent">The log event</param>
        /// <param name="dateTimeOffset">The timestamp of the log event</param>
        /// <returns>The index name to use</returns>
        public string Decide(LogEvent logEvent, DateTimeOffset dateTimeOffset)
        {
            // Check SourceContext to determine log type
            if (logEvent.Properties.TryGetValue(SourceContext, out var sourceContextValue))
            {
                if (sourceContextValue is ScalarValue scalarValue && scalarValue.Value is string sourceContext)
                {
                    // Health check logs
                    if (sourceContext.StartsWith(IHealthCheckNamespace, StringComparison.InvariantCultureIgnoreCase) ||
                        sourceContext.StartsWith(HealthChecksNamespacePrefix, StringComparison.InvariantCultureIgnoreCase) ||
                        IsHealthCheckRequest(logEvent, HealthCheckEvents))
                    {
                        return GetIndexName(Purpose.Healthcheck);
                    }

                    // Exception handler middleware logs
                    if (sourceContext == ExceptionHandlerMiddlewareName)
                    {
                        return GetIndexName(Purpose.Exceptions);
                    }

                    // HTTP logging middleware logs (web requests)
                    if (sourceContext == HttpLoggingMiddlewareName || IsHttpRequestLog(logEvent))
                    {
                        // Check if it's a web body log first
                        if (logEvent.MessageTemplate.Text.Contains(RequestBody, StringComparison.InvariantCultureIgnoreCase) ||
                            logEvent.MessageTemplate.Text.Contains(ResponseBody, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return GetIndexName(Purpose.Webbodies);
                        }
                        
                        // Otherwise route to webrequests index
                        return GetIndexName(Purpose.Webrequests);
                    }

                    // Web request/response body logs (check for request/response body in message)
                    if (logEvent.MessageTemplate.Text.Contains(RequestBody, StringComparison.InvariantCultureIgnoreCase) ||
                        logEvent.MessageTemplate.Text.Contains(ResponseBody, StringComparison.InvariantCultureIgnoreCase))
                    {
                        return GetIndexName(Purpose.Webbodies);
                    }
                }
            }

            // Check for HTTP request properties even without SourceContext
            if (IsHttpRequestLog(logEvent))
            {
                // Check if it's a web body log first
                if (logEvent.MessageTemplate.Text.Contains(RequestBody, StringComparison.InvariantCultureIgnoreCase) ||
                    logEvent.MessageTemplate.Text.Contains(ResponseBody, StringComparison.InvariantCultureIgnoreCase))
                {
                    return GetIndexName(Purpose.Webbodies);
                }
                
                return GetIndexName(Purpose.Webrequests);
            }

            // Default routing based on log level
            if (logEvent.Level >= LogEventLevel.Error)
            {
                return GetIndexName(Purpose.Exceptions);
            }

            return GetIndexName(Purpose.Events);
        }

        private string GetIndexName(Purpose purpose)
        {
            return _indexNameGenerator.GetIndexName(
                purpose.ToString().ToLowerInvariant(),
                _serviceName,
                _serviceEnvironment,
                _logsVersion);
        }

        private static bool IsHealthCheckRequest(LogEvent logEvent, List<string> healthCheckKeywords)
        {
            // Check message template
            if (healthCheckKeywords.Any(x => logEvent.MessageTemplate.Text.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
            {
                return true;
            }

            // Check Uri property
            if (logEvent.Properties.TryGetValue(UriName, out var uriValue))
            {
                var uriString = uriValue.ToString();
                if (healthCheckKeywords.Any(x => uriString.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return true;
                }
            }

            // Check RequestPath property
            if (logEvent.Properties.TryGetValue(RequestPathName, out var requestPathValue))
            {
                var requestPathString = requestPathValue.ToString();
                if (healthCheckKeywords.Any(x => requestPathString.Contains(x, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if a log event is an HTTP request log by checking for HTTP-related properties.
        /// </summary>
        private static bool IsHttpRequestLog(LogEvent logEvent)
        {
            // Check for HTTP method property (GET, POST, PUT, DELETE, etc.)
            if (logEvent.Properties.TryGetValue(HttpMethodName, out var methodValue))
            {
                var methodString = methodValue.ToString();
                var httpMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };
                if (httpMethods.Any(m => methodString.Contains(m, StringComparison.InvariantCultureIgnoreCase)))
                {
                    return true;
                }
            }

            // Check for HTTP status code property
            if (logEvent.Properties.TryGetValue(HttpStatusCodeName, out var statusCodeValue))
            {
                // Status codes are typically 100-599
                var statusCodeString = statusCodeValue.ToString();
                if (int.TryParse(statusCodeString, out var statusCode) && statusCode >= 100 && statusCode <= 599)
                {
                    return true;
                }
            }

            // Check for HTTP path property
            if (logEvent.Properties.TryGetValue(HttpPathName, out var pathValue))
            {
                var pathString = pathValue.ToString();
                // HTTP paths typically start with /
                if (pathString.StartsWith("/", StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            // Check message template for HTTP-related keywords
            var messageText = logEvent.MessageTemplate.Text;
            if (messageText.Contains("HTTP", StringComparison.InvariantCultureIgnoreCase) ||
                messageText.Contains("Request", StringComparison.InvariantCultureIgnoreCase) ||
                messageText.Contains("Response", StringComparison.InvariantCultureIgnoreCase))
            {
                // Additional check: make sure it's not a health check
                if (!IsHealthCheckRequest(logEvent, HealthCheckEvents))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
