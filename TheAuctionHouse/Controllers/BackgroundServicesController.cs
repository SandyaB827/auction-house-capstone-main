using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheAuctionHouse.Services;

namespace TheAuctionHouse.Controllers;

/// <summary>
/// Controller for monitoring and managing background services
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")] // Only admins can access background service management
public class BackgroundServicesController : ControllerBase
{
    private readonly IAuctionExpiryService _auctionExpiryService;
    private readonly ITransactionSettlementService _transactionSettlementService;
    private readonly ILogger<BackgroundServicesController> _logger;

    public BackgroundServicesController(
        IAuctionExpiryService auctionExpiryService,
        ITransactionSettlementService transactionSettlementService,
        ILogger<BackgroundServicesController> logger)
    {
        _auctionExpiryService = auctionExpiryService;
        _transactionSettlementService = transactionSettlementService;
        _logger = logger;
    }

    /// <summary>
    /// Get background services status and statistics
    /// </summary>
    /// <returns>Background services status</returns>
    [HttpGet("status")]
    public async Task<ActionResult<object>> GetStatus()
    {
        try
        {
            var transactionStats = await _transactionSettlementService.GetTransactionStatisticsAsync();
            var expiringAuctions = await _auctionExpiryService.GetAuctionsExpiringWithinAsync(60); // Next hour

            var status = new
            {
                Timestamp = DateTime.UtcNow,
                AuctionExpiry = new
                {
                    AuctionsExpiringWithinHour = expiringAuctions.Count,
                    ExpiringAuctionIds = expiringAuctions
                },
                TransactionSettlement = new
                {
                    TotalTransactions = transactionStats.TotalTransactions,
                    TotalVolume = transactionStats.TotalVolume,
                    UsersWithBlockedAmounts = transactionStats.UsersWithBlockedAmounts,
                    TotalBlockedAmount = transactionStats.TotalBlockedAmount,
                    OrphanedBlockedAmounts = transactionStats.OrphanedBlockedAmounts,
                    LastProcessedTime = transactionStats.LastProcessedTime
                },
                SystemHealth = new
                {
                    Status = "Running",
                    Message = "All background services are operational"
                }
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting background services status");
            return StatusCode(500, "An error occurred while retrieving background services status");
        }
    }

    /// <summary>
    /// Manually trigger auction expiry processing
    /// </summary>
    /// <returns>Processing result</returns>
    [HttpPost("auction-expiry/process")]
    public async Task<ActionResult<object>> ProcessExpiredAuctions()
    {
        try
        {
            _logger.LogInformation("Manual auction expiry processing triggered by admin");
            
            var processedCount = await _auctionExpiryService.ProcessExpiredAuctionsAsync();
            
            var result = new
            {
                Message = "Auction expiry processing completed",
                ProcessedCount = processedCount,
                Timestamp = DateTime.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in manual auction expiry processing");
            return StatusCode(500, "An error occurred while processing expired auctions");
        }
    }

    /// <summary>
    /// Get auctions expiring within specified minutes
    /// </summary>
    /// <param name="withinMinutes">Minutes threshold (default: 60)</param>
    /// <returns>List of expiring auction IDs</returns>
    [HttpGet("auction-expiry/expiring")]
    public async Task<ActionResult<object>> GetExpiringAuctions(int withinMinutes = 60)
    {
        try
        {
            var expiringAuctions = await _auctionExpiryService.GetAuctionsExpiringWithinAsync(withinMinutes);
            
            var result = new
            {
                WithinMinutes = withinMinutes,
                Count = expiringAuctions.Count,
                AuctionIds = expiringAuctions,
                Timestamp = DateTime.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring auctions");
            return StatusCode(500, "An error occurred while retrieving expiring auctions");
        }
    }

    /// <summary>
    /// Force expire a specific auction
    /// </summary>
    /// <param name="auctionId">Auction ID to expire</param>
    /// <returns>Expiry result</returns>
    [HttpPost("auction-expiry/force-expire/{auctionId}")]
    public async Task<ActionResult<object>> ForceExpireAuction(int auctionId)
    {
        try
        {
            _logger.LogInformation("Manual auction expiry triggered for auction {AuctionId} by admin", auctionId);
            
            var success = await _auctionExpiryService.ForceExpireAuctionAsync(auctionId);
            
            if (success)
            {
                var result = new
                {
                    Message = $"Auction {auctionId} has been successfully expired",
                    AuctionId = auctionId,
                    Timestamp = DateTime.UtcNow
                };
                return Ok(result);
            }
            else
            {
                return BadRequest($"Failed to expire auction {auctionId}. Auction may not exist or may not be in Live status.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error force expiring auction {AuctionId}", auctionId);
            return StatusCode(500, "An error occurred while force expiring the auction");
        }
    }

    /// <summary>
    /// Manually trigger wallet reconciliation
    /// </summary>
    /// <returns>Reconciliation result</returns>
    [HttpPost("transaction-settlement/reconcile-wallets")]
    public async Task<ActionResult<object>> ReconcileWallets()
    {
        try
        {
            _logger.LogInformation("Manual wallet reconciliation triggered by admin");
            
            var reconciledCount = await _transactionSettlementService.ReconcileWalletBalancesAsync();
            
            var result = new
            {
                Message = "Wallet reconciliation completed",
                ReconciledCount = reconciledCount,
                Timestamp = DateTime.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in manual wallet reconciliation");
            return StatusCode(500, "An error occurred while reconciling wallets");
        }
    }

    /// <summary>
    /// Manually trigger stuck blocked amounts processing
    /// </summary>
    /// <returns>Processing result</returns>
    [HttpPost("transaction-settlement/process-stuck-amounts")]
    public async Task<ActionResult<object>> ProcessStuckBlockedAmounts()
    {
        try
        {
            _logger.LogInformation("Manual stuck blocked amounts processing triggered by admin");
            
            var processedCount = await _transactionSettlementService.ProcessStuckBlockedAmountsAsync();
            
            var result = new
            {
                Message = "Stuck blocked amounts processing completed",
                ProcessedCount = processedCount,
                Timestamp = DateTime.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in manual stuck blocked amounts processing");
            return StatusCode(500, "An error occurred while processing stuck blocked amounts");
        }
    }

    /// <summary>
    /// Get detailed transaction statistics
    /// </summary>
    /// <returns>Transaction statistics</returns>
    [HttpGet("transaction-settlement/statistics")]
    public async Task<ActionResult<TransactionStatistics>> GetTransactionStatistics()
    {
        try
        {
            var statistics = await _transactionSettlementService.GetTransactionStatisticsAsync();
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction statistics");
            return StatusCode(500, "An error occurred while retrieving transaction statistics");
        }
    }

    /// <summary>
    /// Trigger transaction cleanup (for old records)
    /// </summary>
    /// <param name="olderThanDays">Days threshold for cleanup (default: 365)</param>
    /// <returns>Cleanup result</returns>
    [HttpPost("transaction-settlement/cleanup")]
    public async Task<ActionResult<object>> CleanupOldTransactions(int olderThanDays = 365)
    {
        try
        {
            _logger.LogInformation("Manual transaction cleanup triggered by admin for records older than {Days} days", olderThanDays);
            
            var cleanedCount = await _transactionSettlementService.CleanupOldTransactionsAsync(olderThanDays);
            
            var result = new
            {
                Message = "Transaction cleanup completed",
                CleanedCount = cleanedCount,
                OlderThanDays = olderThanDays,
                Timestamp = DateTime.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in manual transaction cleanup");
            return StatusCode(500, "An error occurred while cleaning up old transactions");
        }
    }
} 