using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentVerificationAPI.Services
{
    public class EmailQueueProcessorService : BackgroundService
    {
        private readonly ILogger<EmailQueueProcessorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _processingInterval;

        public EmailQueueProcessorService(
            ILogger<EmailQueueProcessorService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            
            // Get processing interval from configuration (default: 2 minutes)
            var intervalMinutes = configuration.GetValue<int>("Email:ProcessingIntervalMinutes", 2);
            _processingInterval = TimeSpan.FromMinutes(intervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Queue Processor Service started with interval: {Interval}", _processingInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessEmailQueueAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing email queue");
                }

                // Wait for the next processing cycle
                await Task.Delay(_processingInterval, stoppingToken);
            }

            _logger.LogInformation("Email Queue Processor Service stopped");
        }

        private async Task ProcessEmailQueueAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var processedCount = await emailService.ProcessEmailQueueAsync(batchSize: 10);

                if (processedCount > 0)
                {
                    _logger.LogInformation("Email queue processing completed. Processed {ProcessedCount} emails", processedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in email queue processing cycle");
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Email Queue Processor Service is stopping");
            await base.StopAsync(stoppingToken);
        }
    }
}