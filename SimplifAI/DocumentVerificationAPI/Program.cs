using Microsoft.EntityFrameworkCore;
using DocumentVerificationAPI.Data;
using DocumentVerificationAPI.Services;
using DocumentVerificationAPI.Middleware;
using DocumentVerificationAPI.Filters;
using Serilog;
using Serilog.Events;
using System.Diagnostics;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/application-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Add global model validation filter
    options.Filters.Add<ModelValidationFilter>();
    
    // Add request size validation filter
    options.Filters.Add<RequestSizeValidationFilter>();
    
    // Add rate limiting filter for sensitive endpoints
    // Note: In production, consider using a more sophisticated rate limiting solution
});

// Entity Framework will be configured below with performance optimizations

// Add caching services
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, MemoryCacheService>();

// Add monitoring and performance services
builder.Services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();

// Add Azure AI Foundry service
builder.Services.AddScoped<IAzureAIFoundryService, AzureAIFoundryService>();

// Add application services
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IDocumentVerificationService, EnhancedDocumentVerificationService>();
builder.Services.AddScoped<IAsyncDocumentVerificationService, AsyncDocumentVerificationService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();

// Add background services
builder.Services.AddHostedService<EmailQueueProcessorService>();

// Configure Entity Framework with performance optimizations
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // Enable query splitting for better performance with related data
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"), sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        sqlOptions.CommandTimeout(30);
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Add performance monitoring middleware (should be early in pipeline)
app.UsePerformanceMonitoring();

// Add global exception handling middleware (should be first)
app.UseGlobalExceptionHandling();

// Add input validation middleware
app.UseInputValidation();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowAngularApp");

app.UseAuthorization();

app.MapControllers();

try
{
    Log.Information("Starting Document Verification API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible for integration tests
public partial class Program { }
