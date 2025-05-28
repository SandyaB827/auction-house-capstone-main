namespace TheAuctionHouse.Services;

/// <summary>
/// Interface for transaction settlement background service
/// </summary>
public interface ITransactionSettlementService
{
    /// <summary>
    /// Process pending transaction settlements
    /// </summary>
    /// <returns>Number of transactions processed</returns>
    Task<int> ProcessPendingSettlementsAsync();

    /// <summary>
    /// Clean up old transaction records (older than specified days)
    /// </summary>
    /// <param name="olderThanDays">Days threshold for cleanup</param>
    /// <returns>Number of records cleaned up</returns>
    Task<int> CleanupOldTransactionsAsync(int olderThanDays);

    /// <summary>
    /// Reconcile wallet balances with transaction history
    /// </summary>
    /// <returns>Number of wallets reconciled</returns>
    Task<int> ReconcileWalletBalancesAsync();

    /// <summary>
    /// Get transaction statistics for monitoring
    /// </summary>
    /// <returns>Transaction statistics</returns>
    Task<TransactionStatistics> GetTransactionStatisticsAsync();

    /// <summary>
    /// Process stuck or orphaned blocked amounts
    /// </summary>
    /// <returns>Number of stuck amounts processed</returns>
    Task<int> ProcessStuckBlockedAmountsAsync();
}

/// <summary>
/// Transaction statistics for monitoring
/// </summary>
public class TransactionStatistics
{
    public int TotalTransactions { get; set; }
    public int PendingTransactions { get; set; }
    public decimal TotalVolume { get; set; }
    public int UsersWithBlockedAmounts { get; set; }
    public decimal TotalBlockedAmount { get; set; }
    public int OrphanedBlockedAmounts { get; set; }
    public DateTime LastProcessedTime { get; set; }
} 