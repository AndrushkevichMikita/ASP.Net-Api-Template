using Serilog.Core;
using Serilog.Events;

namespace ApiTemplate.SharedKernel.Logging
{
    /// <summary>
    /// Enriches log events with Elastic Common Schema (ECS) compliant fields.
    /// Replaces non-standard fields with ECS standard equivalents.
    /// </summary>
    public class EcsEnricher : ILogEventEnricher
    {
        private readonly string _serviceName;
        private readonly string _serviceEnvironment;
        private readonly string _serviceVersion;

        public EcsEnricher(string serviceName, string serviceEnvironment, string serviceVersion)
        {
            _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
            _serviceEnvironment = serviceEnvironment ?? throw new ArgumentNullException(nameof(serviceEnvironment));
            _serviceVersion = serviceVersion ?? throw new ArgumentNullException(nameof(serviceVersion));
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            // ECS Service Fields - Replace non-standard fields
            ReplaceProperty(logEvent, propertyFactory, "ElasticApmServiceName", "service.name", _serviceName);
            ReplaceProperty(logEvent, propertyFactory, "Environment", "service.environment", _serviceEnvironment);
            ReplaceProperty(logEvent, propertyFactory, "ElasticApmServiceVersion", "service.version", _serviceVersion);
            
            // Always set ECS service fields (even if old fields don't exist)
            ReplaceOrAddProperty(logEvent, propertyFactory, "service.name", _serviceName);
            ReplaceOrAddProperty(logEvent, propertyFactory, "service.environment", _serviceEnvironment);
            ReplaceOrAddProperty(logEvent, propertyFactory, "service.version", _serviceVersion);

            // ECS Event Fields
            ReplaceOrAddProperty(logEvent, propertyFactory, "event.created", DateTimeOffset.UtcNow);
            ReplaceOrAddProperty(logEvent, propertyFactory, "event.kind", "event");
            ReplaceOrAddProperty(logEvent, propertyFactory, "event.category", "application");

            // ECS Log Fields - Replace level with log.level
            ReplaceProperty(logEvent, propertyFactory, "level", "log.level", logEvent.Level.ToString());
            ReplaceOrAddProperty(logEvent, propertyFactory, "log.level", logEvent.Level.ToString());

            // Map SourceContext to ECS log.logger field
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
            {
                if (sourceContext is ScalarValue scalarValue && scalarValue.Value is string sourceContextValue)
                {
                    ReplaceOrAddProperty(logEvent, propertyFactory, "log.logger", sourceContextValue);
                    RemovePropertyIfPresent(logEvent, "SourceContext");
                }
            }

            // Map MachineName to ECS host.name
            if (logEvent.Properties.TryGetValue("MachineName", out var machineName))
            {
                if (machineName is ScalarValue machineNameValue && machineNameValue.Value is string machineNameString)
                {
                    ReplaceOrAddProperty(logEvent, propertyFactory, "host.name", machineNameString);
                    RemovePropertyIfPresent(logEvent, "MachineName");
                }
            }

            // Map HTTP fields to ECS http.* namespace
            if (logEvent.Properties.TryGetValue("Method", out var method))
            {
                if (method is ScalarValue methodValue && methodValue.Value is string methodString)
                {
                    ReplaceOrAddProperty(logEvent, propertyFactory, "http.request.method", methodString);
                    RemovePropertyIfPresent(logEvent, "Method");
                }
            }

            if (logEvent.Properties.TryGetValue("RequestPath", out var requestPath))
            {
                if (requestPath is ScalarValue pathValue && pathValue.Value is string pathString)
                {
                    ReplaceOrAddProperty(logEvent, propertyFactory, "http.request.path", pathString);
                    RemovePropertyIfPresent(logEvent, "RequestPath");
                }
            }

            if (logEvent.Properties.TryGetValue("StatusCode", out var statusCode))
            {
                if (statusCode is ScalarValue statusValue)
                {
                    // StatusCode might be integer or string
                    var statusCodeValue = statusValue.Value;
                    ReplaceOrAddProperty(logEvent, propertyFactory, "http.response.status_code", statusCodeValue);
                    RemovePropertyIfPresent(logEvent, "StatusCode");
                }
            }

            // Remove other non-standard APM fields
            RemovePropertyIfPresent(logEvent, "ElasticApmServiceName");
            RemovePropertyIfPresent(logEvent, "ElasticApmServiceVersion");
            RemovePropertyIfPresent(logEvent, "ElasticApmServiceNodeName");
            RemovePropertyIfPresent(logEvent, "ElasticApmGlobalLabels");
            RemovePropertyIfPresent(logEvent, "ElasticApmTransactionId");
            RemovePropertyIfPresent(logEvent, "ElasticApmTraceId");
        }

        /// <summary>
        /// Replaces an old property with a new ECS-compliant property.
        /// </summary>
        private static void ReplaceProperty(
            LogEvent logEvent,
            ILogEventPropertyFactory propertyFactory,
            string oldPropertyName,
            string newPropertyName,
            object newPropertyValue)
        {
            if (logEvent.Properties.ContainsKey(oldPropertyName))
            {
                ReplaceOrAddProperty(logEvent, propertyFactory, newPropertyName, newPropertyValue);
                RemovePropertyIfPresent(logEvent, oldPropertyName);
            }
        }

        /// <summary>
        /// Adds or replaces a property (removes old if exists, then adds new).
        /// </summary>
        private static void ReplaceOrAddProperty(
            LogEvent logEvent,
            ILogEventPropertyFactory propertyFactory,
            string propertyName,
            object propertyValue)
        {
            RemovePropertyIfPresent(logEvent, propertyName);
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(propertyName, propertyValue));
        }

        /// <summary>
        /// Removes a property if it exists.
        /// </summary>
        private static void RemovePropertyIfPresent(LogEvent logEvent, string propertyName)
        {
            if (logEvent.Properties.ContainsKey(propertyName))
            {
                logEvent.RemovePropertyIfPresent(propertyName);
            }
        }
    }
}
