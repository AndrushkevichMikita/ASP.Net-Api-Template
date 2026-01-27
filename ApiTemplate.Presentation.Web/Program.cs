using ApiTemplate.Application;
using ApiTemplate.Application.EventHandlers;
using ApiTemplate.Domain;
using ApiTemplate.Domain.Events;
using ApiTemplate.Infrastructure;
using ApiTemplate.Presentation.Web;
using ApiTemplate.SharedKernel;
using ApiTemplate.SharedKernel.CustomPolicy;
using ApiTemplate.SharedKernel.Elasticsearch;
using ApiTemplate.SharedKernel.ExceptionHandler;
using ApiTemplate.SharedKernel.Logging;
using ApiTemplate.SharedKernel.PipelineExtensions;
using ApiTemplate.SharedKernel.Scheduler;
using Elastic.Apm.AspNetCore;
using Elastic.Apm.DiagnosticSource;
using Elastic.Apm.EntityFrameworkCore;
using Elastic.Apm.SerilogEnricher;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.Elasticsearch;
using System.Reflection;
using System.Text;
using ApiTemplate.EventBus.Kafka;
using ApiTemplate.EventBus.Abstractions;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.WebHost.ConfigureKestrel(x =>
    {
        // WARN: this options works only if run on Linux or *.exe. If you run on IIS see web.config
        x.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
        x.Limits.MaxRequestBodySize = 2 * Config.MaxRequestSizeBytes; // the real limitations see in Startup...FormOptions // https://github.com/dotnet/aspnetcore/issues/20369
    });
    // applying appsettings
    builder.Configuration.ApplyConfiguration();

    LoggerConfiguration ProvideConfiguration(LoggerConfiguration l = null)
    {
        l ??= new LoggerConfiguration();

        // Get configuration values (needed for ECS enricher and index decider)
        var serviceName = builder.Configuration["ElasticApm:ServiceName"]
            ?? throw new InvalidOperationException(
                "Configuration value 'ElasticApm:ServiceName' is required. Please set it in appsettings.json or environment variables.");

        var serviceEnvironment = builder.Configuration["ElasticApm:Environment"]
            ?? throw new InvalidOperationException(
                "Configuration value 'ElasticApm:Environment' is required. Please set it in appsettings.json or environment variables.");

        var logsVersion = builder.Configuration["ElasticApm:LogsVersion"]
            ?? throw new InvalidOperationException(
                "Configuration value 'ElasticApm:LogsVersion' is required. Please set it in appsettings.json or environment variables.");

        // Create ECS enricher (replaces non-standard fields with ECS equivalents)
        var ecsEnricher = new EcsEnricher(serviceName, serviceEnvironment, logsVersion);

        l = l.ReadFrom.Configuration(builder.Configuration)
             .Enrich.FromLogContext()
             .Enrich.WithExceptionDetails()
             .Enrich.WithMachineName()  // Keep for host.name mapping
             .Enrich.WithElasticApmCorrelationInfo()  // Keep for APM correlation (will be mapped to ECS)
             .Enrich.With(ecsEnricher)  // Add ECS enricher - replaces non-standard fields
             .WriteTo.Console()
             .WriteTo.File(@"Logs\log.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31);

        if (!Config.IntegrationTests)
        {
            // Create index decider instance
            var indexDecider = new ElasticsearchIndexDecider(serviceName, serviceEnvironment, logsVersion);

            l = l.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(builder.Configuration["ElasticConfiguration:Uri"]))
            {
                AutoRegisterTemplate = true,
                IndexDecider = indexDecider.Decide,
                InlineFields = true,
                BatchAction = ElasticOpType.Create
            });
        }

        return l;
    }
    ;

    // configure Serilog + Elasticsearch as sink for Serilog
    Log.Logger = new LoggerConfiguration().CreateLogger();
    builder.Host.UseSerilog((ctx, lc) => ProvideConfiguration(lc));

    builder.Services.AddScheduler(new List<SchedulerItem>
    {
        // new SchedulerItem { TaskType = typeof(SomeName) }, // WARN: It's example of registration
    });

    builder.Services.AddPresentation(builder.Configuration)
                    .AddApplicationServices()
                    .AddInfrastructure(builder.Configuration)
                    .AddDomain(builder.Configuration)
                    .AddSharedKernel()
                    .AddMetrics() // App.Metrics registration
                    .AddKafkaClient(builder.Configuration); // Kafka client registration

    var webApplication = builder.Build();

    webApplication.UseElasticApm(builder.Configuration,
                                 new HttpDiagnosticsSubscriber(),  /* Enable tracing of outgoing HTTP requests */
                                 new EfCoreDiagnosticsSubscriber()); /* Enable tracing of database calls through EF Core*/

    // Configure the HTTP request pipeline.
    if (webApplication.Environment.IsDevelopment())
        webApplication.UseMigrationsEndPoint(); // Error-page with migrations that were not applied
    else
        webApplication.UseHsts();  // The default HSTS value is 30 days.

    webApplication.UseHttpsRedirection();

    webApplication.UseHttpLogging(); // Enable HTTP request/response logging

    webApplication.UseRouting();

    webApplication.UseSwagger(c =>
    {
        c.RouteTemplate = "api/{documentname}/swagger.json";
    });
    // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
    // specifying the Swagger JSON endpoint.
    webApplication.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/api/v1/swagger.json", "asp-web-api template");
        c.RoutePrefix = "api";
    });

    webApplication.UseAuthentication();

    webApplication.UseAuthorization();

    webApplication.HandleExceptions();

    webApplication.UseResponseCaching();
    webApplication.UseResponseCompression();
    webApplication.UseCompressedStaticFiles(webApplication.Environment, webApplication.Services.GetRequiredService<IHttpContextAccessor>());

    webApplication.UseEndpoints(endpoints =>
    {
        // endPoint for SPA
        endpoints.MapFallbackToFile("index.html", new StaticFileOptions
        {
            OnPrepareResponse = x =>
            {
                var httpContext = x.Context;
                var path = httpContext.Request.RouteValues["path"];
                // now you get the original request path
            }
        });

        endpoints.MapHealthChecks("/health");
        endpoints.MapGet("/api/version", async context => await context.Response.WriteAsync(File.ReadAllLines("./appVersion.txt")[0]));
        if (!Config.Production)
        {
            // don't allow it for Production side - because it shares internal IP
            endpoints.MapGet("/myip", async context =>
            {
                var ip = context.Connection.RemoteIpAddress?.MapToIPv4()?.ToString();
                var header = context.Request.Headers["X-Forwarded-For"].ToString();
                var str = new StringBuilder();
                str.Append(ip ?? "null");
                str.AppendLine(" - context.Connection.RemoteIpAddress.MapToIPv4()?.ToString()"); // possible IP of AWS LoadBalancer
                str.Append(string.IsNullOrEmpty(header) ? "null" : header);
                str.AppendLine(" - header 'X-Forwarded-For'"); // header returns ogirinal client IP
                await context.Response.WriteAsync(str.ToString());
            });
        }
        endpoints.MapControllers()
                 //.RequireAuthorization(new AuthorizeAttribute()) // WARN: Enables global [Authorize] attribute for each controller
                 .RequireAuthorization(nameof(IsUserLockedAuthHandler));
    });

    await webApplication.Services.ApplyDbMigrations(builder.Configuration);

    // Register Kafka event subscriptions
    var eventBus = webApplication.Services.GetRequiredService<IEventBus>();
    eventBus.Subscribe<AccountCreatedEvent, AccountCreatedEventHandler>(numberOfConsumers: 1);
    eventBus.Subscribe<AccountUpdatedEvent, AccountUpdatedEventHandler>(numberOfConsumers: 1);

    webApplication.Run();
}
catch (Exception ex)
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Configuration.ApplyConfiguration();
    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
    logger.LogCritical($"Failed to start {Assembly.GetExecutingAssembly().GetName().Name}", ex);
    app.Run(async (context) =>
    {
        await context.Response.WriteAsync(builder.Configuration.GetConnectionString("MSSQL") + ex.Message + Environment.NewLine + ex.StackTrace);
    });
    app.Run();
}

/// <summary>
/// Make the implicit Program class public so test projects can access it
/// </summary>
public partial class Program { }