namespace TheAuctionHouse.Services;

/// <summary>
/// Background service that runs continuously to handle transaction settlement and cleanup
/// </summary>
public class TransactionSettlementBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TransactionSettlementBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _reconciliationInterval;
    private readonly TimeSpan _cleanupInterval;
    private DateTime _lastReconciliation = DateTime.MinValue;
    private DateTime _lastCleanup = DateTime.MinValue;

    public TransactionSettlementBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<TransactionSettlementBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Get intervals from configuration with defaults
        var checkMinutes = configuration.GetValue<int>("BackgroundServices:TransactionSettlementCheckIntervalMinutes", 5);
        var reconciliationHours = configuration.GetValue<int>("BackgroundServices:WalletReconciliationIntervalHours", 24);
        var cleanupHours = configuration.GetValue<int>("BackgroundServices:TransactionCleanupIntervalHours", 168); // 7 days
        
        _checkInterval = TimeSpan.FromMinutes(checkMinutes);
        _reconciliationInterval = TimeSpan.FromHours(reconciliationHours);
        _cleanupInterval = TimeSpan.FromHours(cleanupHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Transaction Settlement Background Service started. Check interval: {Interval}", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTransactionTasks();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in Transaction Settlement Background Service");
            }

            // Wait for the next check interval
            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Transaction Settlement Background Service stopped");
    }

    private async Task ProcessTransactionTasks()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settlementService = scope.ServiceProvider.GetRequiredService<ITransactionSettlementService>();

            // Always process pending settlements and stuck amounts
            await ProcessPendingSettlements(settlementService);
            await ProcessStuckBlockedAmounts(settlementService);

            // Periodic tasks based on intervals
            await ProcessPeriodicTasks(settlementService);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing transaction tasks in background service");
        }
    }

    private async Task ProcessPendingSettlements(ITransactionSettlementService settlementService)
    {
        try
        {
            var processedCount = await settlementService.ProcessPendingSettlementsAsync();
            if (processedCount > 0)
            {
                _logger.LogInformation("Processed {Count} pending settlements", processedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending settlements");
        }
    }

    private async Task ProcessStuckBlockedAmounts(ITransactionSettlementService settlementService)
    {
        try
        {
            var processedCount = await settlementService.ProcessStuckBlockedAmountsAsync();
            if (processedCount > 0)
            {
                _logger.LogInformation("Processed {Count} stuck blocked amounts", processedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing stuck blocked amounts");
        }
    }

    private async Task ProcessPeriodicTasks(ITransactionSettlementService settlementService)
    {
        var now = DateTime.UtcNow;

        // Wallet reconciliation
        if (now - _lastReconciliation >= _reconciliationInterval)
        {
            try
            {
                _logger.LogInformation("Starting wallet reconciliation");
                var reconciledCount = await settlementService.ReconcileWalletBalancesAsync();
                _lastReconciliation = now;
                
                if (reconciledCount > 0)
                {
                    _logger.LogInformation("Wallet reconciliation completed: {Count} wallets reconciled", reconciledCount);
                }
                else
                {
                    _logger.LogInformation("Wallet reconciliation completed: All wallets are in sync");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during wallet reconciliation");
            }
        }

        // Transaction cleanup
        if (now - _lastCleanup >= _cleanupInterval)
        {
            try
            {
                _logger.LogInformation("Starting transaction cleanup");
                var cleanupDays = 365; // Keep transactions for 1 year
                var cleanedCount = await settlementService.CleanupOldTransactionsAsync(cleanupDays);
                _lastCleanup = now;
                
                _logger.LogInformation("Transaction cleanup completed: {Count} records processed", cleanedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during transaction cleanup");
            }
        }

        // Log statistics periodically (every hour)
        if (now.Minute == 0) // Top of the hour
        {
            try
            {
                var stats = await settlementService.GetTransactionStatisticsAsync();
                _logger.LogInformation("Transaction Statistics: Total={Total}, Volume=${Volume:F2}, BlockedUsers={BlockedUsers}, TotalBlocked=${TotalBlocked:F2}, Orphaned={Orphaned}",
                    stats.TotalTransactions, stats.TotalVolume, stats.UsersWithBlockedAmounts, stats.TotalBlockedAmount, stats.OrphanedBlockedAmounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction statistics");
            }
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Transaction Settlement Background Service is stopping");
        await base.StopAsync(stoppingToken);
    }
} 