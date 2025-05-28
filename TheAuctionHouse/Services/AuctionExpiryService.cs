using Microsoft.EntityFrameworkCore;
using TheAuctionHouse.Data.EFCore.SQLite;
using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Services;

/// <summary>
/// Service for handling auction expiry processing and transaction settlement
/// </summary>
public class AuctionExpiryService : IAuctionExpiryService
{
    private readonly AuctionHouseDbContext _context;
    private readonly ILogger<AuctionExpiryService> _logger;

    public AuctionExpiryService(
        AuctionHouseDbContext context,
        ILogger<AuctionExpiryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Process expired auctions and handle settlements
    /// </summary>
    /// <returns>Number of auctions processed</returns>
    public async Task<int> ProcessExpiredAuctionsAsync()
    {
        try
        {
            var currentTime = DateTime.UtcNow;
            var expiredAuctions = await _context.Auctions
                .Include(a => a.Asset)
                .Include(a => a.Seller)
                .Include(a => a.CurrentHighestBidder)
                .Where(a => a.Status == AuctionStatus.Live && 
                           a.StartDate.AddMinutes(a.TotalMinutesToExpiry) <= currentTime)
                .ToListAsync();

            _logger.LogInformation("Found {Count} expired auctions to process", expiredAuctions.Count);

            int processedCount = 0;

            foreach (var auction in expiredAuctions)
            {
                try
                {
                    await ProcessSingleExpiredAuction(auction);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing expired auction {AuctionId}", auction.Id);
                    // Continue processing other auctions even if one fails
                }
            }

            if (processedCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully processed {ProcessedCount} expired auctions", processedCount);
            }

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessExpiredAuctionsAsync");
            return 0;
        }
    }

    /// <summary>
    /// Get auctions that are about to expire (within specified minutes)
    /// </summary>
    /// <param name="withinMinutes">Minutes threshold for upcoming expiry</param>
    /// <returns>List of auction IDs that will expire soon</returns>
    public async Task<List<int>> GetAuctionsExpiringWithinAsync(int withinMinutes)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(withinMinutes);

            var currentTime = DateTime.UtcNow;
            var expiringAuctions = await _context.Auctions
                .Where(a => a.Status == AuctionStatus.Live && 
                           a.StartDate.AddMinutes(a.TotalMinutesToExpiry) > currentTime &&
                           a.StartDate.AddMinutes(a.TotalMinutesToExpiry) <= cutoffTime)
                .Select(a => a.Id)
                .ToListAsync();

            return expiringAuctions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auctions expiring within {Minutes} minutes", withinMinutes);
            return new List<int>();
        }
    }

    /// <summary>
    /// Force expire a specific auction (for manual intervention)
    /// </summary>
    /// <param name="auctionId">Auction ID to expire</param>
    /// <returns>True if successfully expired</returns>
    public async Task<bool> ForceExpireAuctionAsync(int auctionId)
    {
        try
        {
            var auction = await _context.Auctions
                .Include(a => a.Asset)
                .Include(a => a.Seller)
                .Include(a => a.CurrentHighestBidder)
                .FirstOrDefaultAsync(a => a.Id == auctionId && a.Status == AuctionStatus.Live);

            if (auction == null)
            {
                _logger.LogWarning("Auction {AuctionId} not found or not in Live status", auctionId);
                return false;
            }

            await ProcessSingleExpiredAuction(auction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully force expired auction {AuctionId}", auctionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error force expiring auction {AuctionId}", auctionId);
            return false;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Process a single expired auction
    /// </summary>
    /// <param name="auction">The auction to process</param>
    private async Task ProcessSingleExpiredAuction(Auction auction)
    {
        _logger.LogInformation("Processing expired auction {AuctionId}", auction.Id);

        if (auction.CurrentHighestBid > 0 && !string.IsNullOrEmpty(auction.CurrentHighestBidderId))
        {
            // Auction has bids - process successful sale
            await ProcessSuccessfulAuction(auction);
        }
        else
        {
            // No bids - process unsuccessful auction
            await ProcessUnsuccessfulAuction(auction);
        }
    }

    /// <summary>
    /// Process auction that ended with bids (successful sale)
    /// </summary>
    /// <param name="auction">The auction to process</param>
    private async Task ProcessSuccessfulAuction(Auction auction)
    {
        _logger.LogInformation("Processing successful auction {AuctionId} with winning bid ${Amount}", 
            auction.Id, auction.CurrentHighestBid);

        // Update auction status
        auction.Status = AuctionStatus.Expired;

        // Transfer asset ownership (Business Rule 2)
        if (auction.Asset != null && !string.IsNullOrEmpty(auction.CurrentHighestBidderId))
        {
            var previousOwnerId = auction.Asset.OwnerId;
            auction.Asset.OwnerId = auction.CurrentHighestBidderId;
            auction.Asset.Status = AssetStatus.OpenToAuction; // New owner can auction again

            _logger.LogInformation("Asset {AssetId} ownership transferred from {PreviousOwner} to {NewOwner}", 
                auction.Asset.Id, previousOwnerId, auction.CurrentHighestBidderId);
        }

        // Process payment settlement
        await ProcessPaymentSettlement(auction);

        // Release blocked amounts for other bidders
        await ReleaseBlockedAmountsForOtherBidders(auction);
    }

    /// <summary>
    /// Process auction that ended without bids
    /// </summary>
    /// <param name="auction">The auction to process</param>
    private async Task ProcessUnsuccessfulAuction(Auction auction)
    {
        _logger.LogInformation("Processing unsuccessful auction {AuctionId} (no bids)", auction.Id);

        // Update auction status
        auction.Status = AuctionStatus.ExpiredWithoutBids;

        // Return asset to OpenToAuction status (SRS 4.1.5)
        if (auction.Asset != null)
        {
            auction.Asset.Status = AssetStatus.OpenToAuction;
            _logger.LogInformation("Asset {AssetId} returned to OpenToAuction status", auction.Asset.Id);
        }
    }

    /// <summary>
    /// Process payment settlement between winner and seller
    /// </summary>
    /// <param name="auction">The auction to process</param>
    private async Task ProcessPaymentSettlement(Auction auction)
    {
        if (string.IsNullOrEmpty(auction.CurrentHighestBidderId))
            return;

        var winner = await _context.Users.FirstOrDefaultAsync(u => u.Id == auction.CurrentHighestBidderId);
        var seller = await _context.Users.FirstOrDefaultAsync(u => u.Id == auction.SellerId);

        if (winner == null || seller == null)
        {
            _logger.LogError("Could not find winner or seller for auction {AuctionId}", auction.Id);
            return;
        }

        var saleAmount = auction.CurrentHighestBid;

        // Deduct from winner's wallet (amount was already blocked)
        winner.BlockedAmount -= saleAmount;
        winner.WalletBalance -= saleAmount;

        // Add to seller's wallet
        seller.WalletBalance += saleAmount;

        // Create wallet transactions for audit trail
        await CreateWalletTransaction(winner.Id, WalletTransactionType.PaymentMade, saleAmount, 
            $"Payment for winning auction #{auction.Id} - {auction.Asset?.Title}");

        await CreateWalletTransaction(seller.Id, WalletTransactionType.PaymentReceived, saleAmount, 
            $"Payment received from auction #{auction.Id} - {auction.Asset?.Title}");

        _logger.LogInformation("Payment settlement completed for auction {AuctionId}: ${Amount} from {Winner} to {Seller}", 
            auction.Id, saleAmount, winner.Email, seller.Email);
    }

    /// <summary>
    /// Release blocked amounts for bidders who didn't win
    /// </summary>
    /// <param name="auction">The auction to process</param>
    private async Task ReleaseBlockedAmountsForOtherBidders(Auction auction)
    {
        // Get all unique bidders except the winner
        var otherBidders = await _context.BidHistories
            .Where(bh => bh.AuctionId == auction.Id && bh.BidderId != auction.CurrentHighestBidderId)
            .Select(bh => bh.BidderId)
            .Distinct()
            .ToListAsync();

        foreach (var bidderId in otherBidders)
        {
            // Get the highest bid amount for this bidder
            var highestBidAmount = await _context.BidHistories
                .Where(bh => bh.AuctionId == auction.Id && bh.BidderId == bidderId)
                .MaxAsync(bh => bh.BidAmount);

            var bidder = await _context.Users.FirstOrDefaultAsync(u => u.Id == bidderId);
            if (bidder != null)
            {
                // Release the blocked amount
                bidder.BlockedAmount -= highestBidAmount;

                // Create transaction record
                await CreateWalletTransaction(bidderId, WalletTransactionType.BidReleased, highestBidAmount, 
                    $"Bid amount released from expired auction #{auction.Id} - {auction.Asset?.Title}");

                _logger.LogInformation("Released ${Amount} blocked amount for bidder {BidderId} from auction {AuctionId}", 
                    highestBidAmount, bidderId, auction.Id);
            }
        }
    }

    /// <summary>
    /// Create a wallet transaction record
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="type">Transaction type</param>
    /// <param name="amount">Transaction amount</param>
    /// <param name="description">Transaction description</param>
    private async Task CreateWalletTransaction(string userId, WalletTransactionType type, decimal amount, string description)
    {
        var transaction = new WalletTransaction
        {
            UserId = userId,
            Type = type,
            Amount = amount,
            Description = description,
            TransactionDate = DateTime.UtcNow
        };

        _context.WalletTransactions.Add(transaction);
    }

    #endregion
} 