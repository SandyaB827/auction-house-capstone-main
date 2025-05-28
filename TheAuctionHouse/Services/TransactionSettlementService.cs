using Microsoft.EntityFrameworkCore;
using TheAuctionHouse.Data.EFCore.SQLite;
using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Services;

/// <summary>
/// Service for handling transaction settlement, cleanup, and reconciliation
/// </summary>
public class TransactionSettlementService : ITransactionSettlementService
{
    private readonly AuctionHouseDbContext _context;
    private readonly ILogger<TransactionSettlementService> _logger;

    public TransactionSettlementService(
        AuctionHouseDbContext context,
        ILogger<TransactionSettlementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Process pending transaction settlements
    /// </summary>
    /// <returns>Number of transactions processed</returns>
    public async Task<int> ProcessPendingSettlementsAsync()
    {
        try
        {
            // For now, this is a placeholder as our current implementation
            // processes settlements immediately. This could be extended for
            // more complex settlement scenarios like delayed payments, escrow, etc.
            
            _logger.LogDebug("Processing pending settlements - no pending settlements found");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending settlements");
            return 0;
        }
    }

    /// <summary>
    /// Clean up old transaction records (older than specified days)
    /// </summary>
    /// <param name="olderThanDays">Days threshold for cleanup</param>
    /// <returns>Number of records cleaned up</returns>
    public async Task<int> CleanupOldTransactionsAsync(int olderThanDays)
    {
        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
            
            // For audit purposes, we typically don't delete transaction records
            // Instead, we could archive them or mark them as archived
            // This is a conservative approach for financial data
            
            var oldTransactionCount = await _context.WalletTransactions
                .CountAsync(t => t.TransactionDate < cutoffDate);

            _logger.LogInformation("Found {Count} old transactions (older than {Days} days) - keeping for audit purposes", 
                oldTransactionCount, olderThanDays);

            // In a real-world scenario, you might:
            // 1. Archive to a separate table/database
            // 2. Export to external storage
            // 3. Mark as archived without deletion
            
            return 0; // Not deleting for audit compliance
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old transactions");
            return 0;
        }
    }

    /// <summary>
    /// Reconcile wallet balances with transaction history
    /// </summary>
    /// <returns>Number of wallets reconciled</returns>
    public async Task<int> ReconcileWalletBalancesAsync()
    {
        try
        {
            var users = await _context.Users.ToListAsync();
            int reconciledCount = 0;

            foreach (var user in users)
            {
                var transactions = await _context.WalletTransactions
                    .Where(t => t.UserId == user.Id)
                    .ToListAsync();

                // Calculate expected balance from transaction history
                var expectedBalance = CalculateExpectedBalance(transactions);
                var expectedBlockedAmount = await CalculateExpectedBlockedAmount(user.Id);

                bool needsReconciliation = false;

                // Check wallet balance
                if (Math.Abs(user.WalletBalance - expectedBalance) > 0.01m) // Allow for small rounding differences
                {
                    _logger.LogWarning("Wallet balance mismatch for user {UserId}: Current={Current}, Expected={Expected}", 
                        user.Id, user.WalletBalance, expectedBalance);
                    
                    user.WalletBalance = expectedBalance;
                    needsReconciliation = true;
                }

                // Check blocked amount
                if (Math.Abs(user.BlockedAmount - expectedBlockedAmount) > 0.01m)
                {
                    _logger.LogWarning("Blocked amount mismatch for user {UserId}: Current={Current}, Expected={Expected}", 
                        user.Id, user.BlockedAmount, expectedBlockedAmount);
                    
                    user.BlockedAmount = expectedBlockedAmount;
                    needsReconciliation = true;
                }

                if (needsReconciliation)
                {
                    reconciledCount++;
                    _logger.LogInformation("Reconciled wallet for user {UserId}", user.Id);
                }
            }

            if (reconciledCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Reconciled {Count} wallets", reconciledCount);
            }

            return reconciledCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconciling wallet balances");
            return 0;
        }
    }

    /// <summary>
    /// Get transaction statistics for monitoring
    /// </summary>
    /// <returns>Transaction statistics</returns>
    public async Task<TransactionStatistics> GetTransactionStatisticsAsync()
    {
        try
        {
            var totalTransactions = await _context.WalletTransactions.CountAsync();
            var totalVolume = await _context.WalletTransactions
                .Where(t => t.Type == WalletTransactionType.PaymentMade || t.Type == WalletTransactionType.PaymentReceived)
                .SumAsync(t => t.Amount);

            var usersWithBlockedAmounts = await _context.Users
                .CountAsync(u => u.BlockedAmount > 0);

            var totalBlockedAmount = await _context.Users
                .SumAsync(u => u.BlockedAmount);

            var orphanedBlockedAmounts = await GetOrphanedBlockedAmountsCount();

            return new TransactionStatistics
            {
                TotalTransactions = totalTransactions,
                PendingTransactions = 0, // No pending transactions in current implementation
                TotalVolume = totalVolume,
                UsersWithBlockedAmounts = usersWithBlockedAmounts,
                TotalBlockedAmount = totalBlockedAmount,
                OrphanedBlockedAmounts = orphanedBlockedAmounts,
                LastProcessedTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction statistics");
            return new TransactionStatistics { LastProcessedTime = DateTime.UtcNow };
        }
    }

    /// <summary>
    /// Process stuck or orphaned blocked amounts
    /// </summary>
    /// <returns>Number of stuck amounts processed</returns>
    public async Task<int> ProcessStuckBlockedAmountsAsync()
    {
        try
        {
            int processedCount = 0;

            // Find users with blocked amounts but no active bids
            var usersWithBlockedAmounts = await _context.Users
                .Where(u => u.BlockedAmount > 0)
                .ToListAsync();

            foreach (var user in usersWithBlockedAmounts)
            {
                // Check if user has any active bids
                var currentTime = DateTime.UtcNow;
                var activeBids = await _context.BidHistories
                    .Include(bh => bh.Auction)
                    .Where(bh => bh.BidderId == user.Id && 
                                bh.Auction.Status == AuctionStatus.Live && 
                                bh.Auction.StartDate.AddMinutes(bh.Auction.TotalMinutesToExpiry) > currentTime)
                    .ToListAsync();

                if (!activeBids.Any())
                {
                    // User has blocked amount but no active bids - this might be orphaned
                    _logger.LogWarning("Found potentially orphaned blocked amount for user {UserId}: ${Amount}", 
                        user.Id, user.BlockedAmount);

                    // Check if there are any expired auctions where this user had bids but amounts weren't released
                    var expiredAuctionsWithUserBids = await _context.BidHistories
                        .Include(bh => bh.Auction)
                        .Where(bh => bh.BidderId == user.Id && 
                                    (bh.Auction.Status == AuctionStatus.Expired || 
                                     bh.Auction.Status == AuctionStatus.ExpiredWithoutBids ||
                                     bh.Auction.StartDate.AddMinutes(bh.Auction.TotalMinutesToExpiry) <= currentTime))
                        .GroupBy(bh => bh.AuctionId)
                        .ToListAsync();

                    decimal amountToRelease = 0;

                    foreach (var auctionBids in expiredAuctionsWithUserBids)
                    {
                        var auction = auctionBids.First().Auction;
                        
                        // If user didn't win the auction, their bid amount should be released
                        if (auction.CurrentHighestBidderId != user.Id)
                        {
                            var userHighestBid = auctionBids.Max(bh => bh.BidAmount);
                            amountToRelease += userHighestBid;
                        }
                    }

                    if (amountToRelease > 0 && amountToRelease <= user.BlockedAmount)
                    {
                        // Release the orphaned amount
                        user.BlockedAmount -= amountToRelease;

                        // Create transaction record
                        var transaction = new WalletTransaction
                        {
                            UserId = user.Id,
                            Type = WalletTransactionType.BidReleased,
                            Amount = amountToRelease,
                            Description = "Orphaned blocked amount released by system cleanup",
                            TransactionDate = DateTime.UtcNow
                        };

                        _context.WalletTransactions.Add(transaction);
                        processedCount++;

                        _logger.LogInformation("Released orphaned blocked amount ${Amount} for user {UserId}", 
                            amountToRelease, user.Id);
                    }
                }
            }

            if (processedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Processed {Count} stuck blocked amounts", processedCount);
            }

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing stuck blocked amounts");
            return 0;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Calculate expected wallet balance from transaction history
    /// </summary>
    /// <param name="transactions">User's transaction history</param>
    /// <returns>Expected balance</returns>
    private decimal CalculateExpectedBalance(List<WalletTransaction> transactions)
    {
        decimal balance = 0;

        foreach (var transaction in transactions)
        {
            switch (transaction.Type)
            {
                case WalletTransactionType.Deposit:
                case WalletTransactionType.PaymentReceived:
                case WalletTransactionType.BidReleased:
                    balance += transaction.Amount;
                    break;
                
                case WalletTransactionType.Withdrawal:
                case WalletTransactionType.PaymentMade:
                    balance -= transaction.Amount;
                    break;
                
                case WalletTransactionType.BidBlocked:
                    // Bid blocked doesn't affect wallet balance, only blocked amount
                    break;
            }
        }

        return balance;
    }

    /// <summary>
    /// Calculate expected blocked amount for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Expected blocked amount</returns>
    private async Task<decimal> CalculateExpectedBlockedAmount(string userId)
    {
        // Get user's active bids (auctions that are still live)
        var currentTime = DateTime.UtcNow;
        var activeBids = await _context.BidHistories
            .Include(bh => bh.Auction)
            .Where(bh => bh.BidderId == userId && 
                        bh.Auction.Status == AuctionStatus.Live && 
                        bh.Auction.StartDate.AddMinutes(bh.Auction.TotalMinutesToExpiry) > currentTime)
            .GroupBy(bh => bh.AuctionId)
            .ToListAsync();

        decimal expectedBlockedAmount = 0;

        foreach (var auctionBids in activeBids)
        {
            // Get the highest bid amount for this auction
            var highestBidAmount = auctionBids.Max(bh => bh.BidAmount);
            expectedBlockedAmount += highestBidAmount;
        }

        return expectedBlockedAmount;
    }

    /// <summary>
    /// Get count of orphaned blocked amounts
    /// </summary>
    /// <returns>Count of users with potentially orphaned blocked amounts</returns>
    private async Task<int> GetOrphanedBlockedAmountsCount()
    {
        var usersWithBlockedAmounts = await _context.Users
            .Where(u => u.BlockedAmount > 0)
            .Select(u => u.Id)
            .ToListAsync();

        int orphanedCount = 0;

        foreach (var userId in usersWithBlockedAmounts)
        {
            var hasActiveBids = await _context.BidHistories
                .Include(bh => bh.Auction)
                .AnyAsync(bh => bh.BidderId == userId && 
                               bh.Auction.Status == AuctionStatus.Live && 
                               !bh.Auction.IsExpired());

            if (!hasActiveBids)
            {
                orphanedCount++;
            }
        }

        return orphanedCount;
    }

    #endregion
} 