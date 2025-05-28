namespace TheAuctionHouse.Services;

/// <summary>
/// Interface for auction expiry background service
/// </summary>
public interface IAuctionExpiryService
{
    /// <summary>
    /// Process expired auctions and handle settlements
    /// </summary>
    /// <returns>Number of auctions processed</returns>
    Task<int> ProcessExpiredAuctionsAsync();

    /// <summary>
    /// Get auctions that are about to expire (within specified minutes)
    /// </summary>
    /// <param name="withinMinutes">Minutes threshold for upcoming expiry</param>
    /// <returns>List of auction IDs that will expire soon</returns>
    Task<List<int>> GetAuctionsExpiringWithinAsync(int withinMinutes);

    /// <summary>
    /// Force expire a specific auction (for manual intervention)
    /// </summary>
    /// <param name="auctionId">Auction ID to expire</param>
    /// <returns>True if successfully expired</returns>
    Task<bool> ForceExpireAuctionAsync(int auctionId);
} 