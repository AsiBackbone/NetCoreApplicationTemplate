using System.Diagnostics;
using System.Globalization;
using ProjectTemplate.Web.Authentication.Extensions;
using ProjectTemplate.Web.ErrorHandling;
using ProjectTemplate.Web.Extensions;
using Serilog;

Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Debug(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] [CorrelationId: {CorrelationId}] [TraceId: {TraceId}] [SpanId: {SpanId}] [RequestId: {RequestId}] [RequestPath: {RequestPath}] {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{SourceContext}] [CorrelationId: {CorrelationId}] [TraceId: {TraceId}] [SpanId: {SpanId}] [RequestId: {RequestId}] [RequestPath: {RequestPath}] {Message:lj}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.AddApplicationSerilog();
    Log.Information("Bootstrapping Template.Web application");
    builder.Services.AddControllersWithViews();
    builder.Services.AddApplicationApiVersioning(builder.Configuration);
    builder.Services.AddRazorPages();
    builder.Services.AddApplicationHealthChecks();
    builder.Services.AddApplicationForwardedHeaders(builder.Configuration);
    builder.Services.AddApplicationSecurityHeaders(builder.Configuration);
    builder.Services.AddApplicationRateLimiting(builder.Configuration, builder.Environment);
    builder.Services.AddApplicationRequestLogging(builder.Configuration);
    builder.Services.AddApplicationOpenTelemetry(builder.Configuration, builder.Environment);
    builder.Services.AddApplicationProblemDetails(builder.Environment);
    builder.Services.AddApplicationDataProtection(builder.Configuration, builder.Environment);
    builder.Services.AddApplicationAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddApplicationAuthorization(builder.Configuration);
    builder.Services.AddApplicationDataAccess(builder.Configuration);

    Log.Information("Starting ProjectTemplate.Web application");
    WebApplication app = builder.Build();

    Log.Information("Configuring pipeline for ProjectTemplate.Web application");
    app.UseApplicationPipeline();
    app.MapApplicationHealthChecks();

    Log.Information("Running ProjectTemplate.Web application");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ProjectTemplate.Web application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
