using ApiTemplate.Application;
using ApiTemplate.Domain;
using ApiTemplate.Domain.Services;
using ApiTemplate.Infrastructure;
using ApiTemplate.Presentation.Web;
using ApiTemplate.SharedKernel;
using ApiTemplate.SharedKernel.CustomPolicy;
using ApiTemplate.SharedKernel.ExceptionHandler;
using ApiTemplate.SharedKernel.PipelineExtensions;
using ApiTemplate.SharedKernel.Scheduler;
using Elastic.Apm.AspNetCore;
using Elastic.Apm.DiagnosticSource;
using Elastic.Apm.EntityFrameworkCore;
using Elastic.Apm.SerilogEnricher;
using Elastic.CommonSchema.Serilog;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Serilog.Exceptions;
using Serilog.Sinks.Elasticsearch;
using System.Reflection;
using System.Text;

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

    LoggerConfiguration ProvideConfiguration(LoggerConfiguration l)
    {
        l = l.ReadFrom.Configuration(builder.Configuration)
              .Enrich.FromLogContext()
              .Enrich.WithExceptionDetails()
              .Enrich.WithMachineName()
              .Enrich.WithElasticApmCorrelationInfo()
              .Enrich.WithProperty("Environment", Config.Env)
              .WriteTo.Console()
              .WriteTo.File(@"Logs\log.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 31);

        if (!Config.IntegrationTests)
            l = l.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(builder.Configuration["ElasticConfiguration:Uri"]))
            {
                AutoRegisterTemplate = true,
                CustomFormatter = new EcsTextFormatter(),
                IndexFormat = $"{Assembly.GetExecutingAssembly().GetName().Name.ToLower().Replace(".", "-")}-{Config.Env?.ToLower().Replace(".", "-")}-{DateTime.UtcNow:yyyy-MM}"
            });

        return l;
    };

    // configure Serilog + Elasticsearch as sink for Serilog
    builder.Host.UseSerilog((ctx, lc) => ProvideConfiguration(lc));

    builder.Services.AddScheduler(new List<SchedulerItem>
    {
        // new SchedulerItem { TaskType = typeof(SomeName) }, // WARN: It's example of registration
    });

    builder.Services.AddPresentation(builder.Configuration)
                    .AddApplicationServices(builder.Configuration, IdentityConstants.ApplicationScheme)
                    .AddInfrastructure<ApplicationUserClaimsPrincipalFactory>(builder.Configuration)
                    .AddDomain(builder.Configuration)
                    .AddSharedKernel();

    var webApplication = builder.Build();

    webApplication.UseHttpLogging();

    webApplication.UseElasticApm(builder.Configuration,
                                 new HttpDiagnosticsSubscriber(),  /* Enable tracing of outgoing HTTP requests */
                                 new EfCoreDiagnosticsSubscriber()); /* Enable tracing of database calls through EF Core*/

    // Configure the HTTP request pipeline.
    if (webApplication.Environment.IsDevelopment())
        webApplication.UseMigrationsEndPoint(); // Error-page with migrations that were not applied
    else
        webApplication.UseHsts();  // The default HSTS value is 30 days.

    webApplication.UseHttpsRedirection();

    webApplication.UseRouting();

    if (!Config.Production)
    {
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
    }

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