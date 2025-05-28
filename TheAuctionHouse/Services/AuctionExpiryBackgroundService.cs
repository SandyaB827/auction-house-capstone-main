namespace TheAuctionHouse.Services;

/// <summary>
/// Background service that runs continuously to process expired auctions
/// </summary>
public class AuctionExpiryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuctionExpiryBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;

    public AuctionExpiryBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AuctionExpiryBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Get check interval from configuration, default to 1 minute
        var intervalMinutes = configuration.GetValue<int>("BackgroundServices:AuctionExpiryCheckIntervalMinutes", 1);
        _checkInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auction Expiry Background Service started. Check interval: {Interval}", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredAuctions();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in Auction Expiry Background Service");
            }

            // Wait for the next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Auction Expiry Background Service stopped");
    }

    private async Task ProcessExpiredAuctions()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var auctionExpiryService = scope.ServiceProvider.GetRequiredService<IAuctionExpiryService>();

            var processedCount = await auctionExpiryService.ProcessExpiredAuctionsAsync();

            if (processedCount > 0)
            {
                _logger.LogInformation("Processed {Count} expired auctions", processedCount);
            }
            else
            {
                _logger.LogDebug("No expired auctions found to process");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expired auctions in background service");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auction Expiry Background Service is stopping");
        await base.StopAsync(stoppingToken);
    }
} 