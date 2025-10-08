using Microsoft.Extensions.Hosting;

namespace SalesAnalysis.Services
{
    public class DataRefreshBackgroundService : BackgroundService
    {
        private readonly ILogger<DataRefreshBackgroundService> _logger;
        private readonly IBackgroundJobQueue _queue;
        private readonly ICsvLoaderService _loader;
        private readonly IConfiguration _config;

        public DataRefreshBackgroundService(ILogger<DataRefreshBackgroundService> logger,
                                            IBackgroundJobQueue queue,
                                            ICsvLoaderService loader,
                                            IConfiguration config)
        {
            _logger = logger;
            _queue = queue;
            _loader = loader;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Start a scheduled timer for daily refresh (simple)
            var dailyRefreshPath = _config.GetValue<string>("DailyRefresh:FilePath");
            var refreshTime = _config.GetValue<TimeSpan?>("DailyRefresh:TimeOfDay") ?? TimeSpan.FromHours(2);

            _logger.LogInformation("Background service started. Scheduled daily refresh at {Time}", refreshTime);

            // Worker loop to process queued jobs
            var processingTask = Task.Run(async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var job = await _queue.DequeueAsync(stoppingToken);
                    try
                    {
                        await job(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Queued job failed.");
                    }
                }
            }, stoppingToken);

            // simple scheduled daily run (UTC)
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var nextRun = DateTime.UtcNow.Date.Add(refreshTime);
                    if (nextRun <= now) nextRun = nextRun.AddDays(1);
                    var delay = nextRun - now;
                    await Task.Delay(delay, stoppingToken);

                    if (!string.IsNullOrEmpty(dailyRefreshPath))
                    {
                        _logger.LogInformation("Starting scheduled daily refresh from {Path}", dailyRefreshPath);
                        await _loader.LoadCsvFileAsync(dailyRefreshPath, stoppingToken);
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scheduled refresh failed.");
                }
            }

            await processingTask;
        }
    }
}
