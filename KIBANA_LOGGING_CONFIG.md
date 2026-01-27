# Kibana/Elasticsearch Logging Configuration

## Overview

This application uses **Serilog** with **Elasticsearch** sink for centralized logging. Logs are automatically routed to different Elasticsearch indices based on log type, following ECS (Elastic Common Schema) standards.

## Index Naming Pattern

```
logs-{serviceName}-{environment}-{version}-{purpose}
```

**Examples:**

- `logs-myapp-development-v1-exceptions`
- `logs-myapp-development-v1-webrequests`
- `logs-myapp-development-v1-events`
- `logs-myapp-development-v1-healthcheck`

## Log Routing (IndexDecider)

Logs are automatically routed to appropriate indices based on:

| Log Type | Index Purpose | Detection Criteria |
|----------|---------------|-------------------|
| **Exceptions** | `exceptions` | Error level logs, ExceptionHandlerMiddleware, or any log with level ≥ Error |
| **Web Requests** | `webrequests` | HTTP logging middleware, HTTP method/status code/path properties |
| **Web Bodies** | `webbodies` | Request/response body logs |
| **Health Checks** | `healthcheck` | Health check namespace or healthcheck/healthz keywords |
| **Events** | `events` | Default for non-error logs that don't match other criteria |

## ECS Field Enrichment

All logs are enriched with **Elastic Common Schema (ECS)** compliant fields:

- **Service Fields**: `service.name`, `service.environment`, `service.version`
- **Event Fields**: `event.created`, `event.kind`, `event.category`
- **Log Fields**: `log.level`, `log.logger`
- **Host Fields**: `host.name`
- **HTTP Fields**: `http.request.method`, `http.request.path`, `http.response.status_code`

**Non-standard fields are replaced** (not duplicated):

- `ElasticApmServiceName` → `service.name`
- `Environment` → `service.environment`
- `level` → `log.level`
- `SourceContext` → `log.logger`
- `MachineName` → `host.name`
- `Method` → `http.request.method`
- `RequestPath` → `http.request.path`
- `StatusCode` → `http.response.status_code`

## Required Configuration

These configuration values are **mandatory** (application will fail to start if missing):

```json
{
  "ElasticApm": {
    "ServiceName": "myapp",           // Required
    "Environment": "development",     // Required
    "LogsVersion": "v1"               // Required
  },
  "ElasticConfiguration": {
    "Uri": "http://localhost:9200"   // Required
  }
}
```

## Kibana Index Patterns

Recommended index patterns to create in Kibana:

- `logs-*-exceptions` - Exception logs
- `logs-*-webrequests` - HTTP request logs
- `logs-*-healthcheck` - Health check logs
- `logs-*-webbodies` - Request/response bodies
- `logs-*-events` - General events
- `logs-*-*` - All logs

**Time Field**: Always use `@timestamp` (not `event.created`)

## Key Features

- ✅ **Automatic index routing** based on log type
- ✅ **ECS-compliant fields** for cross-service queries
- ✅ **HTTP logging middleware** enabled for request/response logging
- ✅ **Elastic APM integration** with correlation IDs
- ✅ **Index templates** auto-registered by Serilog

## Implementation Files

- `ApiTemplate.SharedKernel/Elasticsearch/ElasticsearchIndexDecider.cs` - Routing logic
- `ApiTemplate.SharedKernel/Elasticsearch/IndexNameGenerator.cs` - Index name generation
- `ApiTemplate.SharedKernel/Logging/EcsEnricher.cs` - ECS field enrichment
- `ApiTemplate.Presentation.Web/Program.cs` - Serilog configuration

## Query Examples

**Query by service:**

```json
GET logs-*/_search
{
  "query": {
    "term": { "service.name": "myapp" }
  }
}
```

**Query errors:**

```json
GET logs-*/_search
{
  "query": {
    "term": { "log.level": "Error" }
  }
}
```

**Query HTTP requests:**

```json
GET logs-*/_search
{
  "query": {
    "exists": { "field": "http.request.method" }
  }
}
```
