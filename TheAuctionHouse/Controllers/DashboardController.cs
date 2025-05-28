using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TheAuctionHouse.Data.EFCore.SQLite;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Models;

namespace TheAuctionHouse.Controllers;

/// <summary>
/// Controller for dashboard functionality in the auction house system
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // All endpoints require authentication
public class DashboardController : ControllerBase
{
    private readonly AuctionHouseDbContext _context;
    private readonly UserManager<PortalUser> _userManager;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        AuctionHouseDbContext context,
        UserManager<PortalUser> userManager,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get comprehensive dashboard data for the current user
    /// </summary>
    /// <returns>Complete dashboard information</returns>
    [HttpGet]
    [Authorize(Policy = "AuthenticatedUser")]
    public async Task<ActionResult<DashboardResponse>> GetDashboard()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not found");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            var dashboard = new DashboardResponse
            {
                UserSummary = await GetUserSummary(user),
                WalletSummary = await GetWalletSummary(userId),
                AuctionsSummary = await GetAuctionsSummary(userId),
                AssetsSummary = await GetAssetsSummary(userId),
                ActiveAuctions = await GetActiveAuctionsSummary(userId),
                UserActiveBids = await GetUserActiveBids(userId),
                RecentActivity = await GetRecentActivity(userId)
            };

            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while retrieving dashboard data");
        }
    }

    /// <summary>
    /// Get active auctions list for dashboard
    /// </summary>
    /// <param name="limit">Number of auctions to return (default: 10, max: 50)</param>
    /// <param name="sortBy">Sort by: expiry, bid, created (default: expiry)</param>
    /// <returns>List of active auctions</returns>
    [HttpGet("active-auctions")]
    [Authorize(Policy = "AuthenticatedUser")]
    public async Task<ActionResult<List<ActiveAuctionSummary>>> GetActiveAuctions(int limit = 10, string sortBy = "expiry")
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Validate limit parameter
            limit = Math.Min(50, Math.Max(1, limit));

            var query = _context.Auctions
                .Include(a => a.Asset)
                .Include(a => a.Seller)
                .Include(a => a.CurrentHighestBidder)
                .Include(a => a.BidHistories)
                .Where(a => a.Status == AuctionStatus.Live && !a.IsExpired());

            // Apply sorting
            query = sortBy.ToLower() switch
            {
                "bid" => query.OrderByDescending(a => a.CurrentHighestBidderId == userId ? 1 : 0)
                             .ThenByDescending(a => a.CurrentHighestBid),
                "created" => query.OrderByDescending(a => a.StartDate),
                _ => query.OrderByDescending(a => a.CurrentHighestBidderId == userId ? 1 : 0)
                          .ThenBy(a => a.StartDate.AddMinutes(a.TotalMinutesToExpiry)) // Default: expiry with user bids first
            };

            var auctions = await query.Take(limit).ToListAsync();

            var auctionSummaries = auctions.Select(auction => new ActiveAuctionSummary
            {
                Id = auction.Id,
                AssetTitle = auction.Asset?.Title ?? "Unknown Asset",
                AssetDescription = auction.Asset?.Description ?? "",
                AssetRetailValue = auction.Asset?.RetailValue ?? 0,
                SellerName = $"{auction.Seller?.FirstName} {auction.Seller?.LastName}".Trim(),
                ReservedPrice = auction.ReservedPrice,
                CurrentHighestBid = auction.CurrentHighestBid,
                CurrentHighestBidderName = auction.CurrentHighestBidder != null 
                    ? $"{auction.CurrentHighestBidder.FirstName} {auction.CurrentHighestBidder.LastName}".Trim() 
                    : null,
                MinimumBidIncrement = auction.MinimumBidIncrement,
                StartDate = auction.StartDate,
                ExpiryDate = auction.StartDate.AddMinutes(auction.TotalMinutesToExpiry),
                RemainingTimeInMinutes = auction.GetRemainingTimeInMinutes(),
                Status = auction.Status.ToString(),
                UserIsHighestBidder = auction.CurrentHighestBidderId == userId,
                UserIsSeller = auction.SellerId == userId,
                CanBid = !auction.IsExpired() && auction.SellerId != userId && auction.Status == AuctionStatus.Live,
                TotalBids = auction.BidHistories?.Count ?? 0
            }).ToList();

            return Ok(auctionSummaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active auctions for dashboard");
            return StatusCode(500, "An error occurred while retrieving active auctions");
        }
    }

    /// <summary>
    /// Get user's active bids for dashboard
    /// </summary>
    /// <param name="limit">Number of bids to return (default: 10, max: 50)</param>
    /// <returns>List of user's active bids</returns>
    [HttpGet("my-active-bids")]
    [Authorize(Policy = "BidderOrAdmin")]
    public async Task<ActionResult<List<UserBidSummary>>> GetMyActiveBids(int limit = 10)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Validate limit parameter
            limit = Math.Min(50, Math.Max(1, limit));

            var activeBids = await GetUserActiveBids(userId, limit);

            return Ok(activeBids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active bids for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while retrieving your active bids");
        }
    }

    /// <summary>
    /// Get platform and user statistics (Admin only for platform stats)
    /// </summary>
    /// <returns>Platform and user statistics</returns>
    [HttpGet("stats")]
    [Authorize(Policy = "AuthenticatedUser")]
    public async Task<ActionResult<DashboardStatsResponse>> GetStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = await IsCurrentUserAdmin();

            var stats = new DashboardStatsResponse
            {
                UserStats = await GetUserStats(userId)
            };

            // Only provide platform stats to admins
            if (isAdmin)
            {
                stats.PlatformStats = await GetPlatformStats();
            }

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stats for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while retrieving statistics");
        }
    }

    /// <summary>
    /// Get recent activity for the user
    /// </summary>
    /// <param name="limit">Number of activities to return (default: 20, max: 100)</param>
    /// <returns>List of recent activities</returns>
    [HttpGet("recent-activity")]
    [Authorize(Policy = "AuthenticatedUser")]
    public async Task<ActionResult<List<RecentActivityItem>>> GetRecentActivity(int limit = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // Validate limit parameter
            limit = Math.Min(100, Math.Max(1, limit));

            var activities = await GetRecentActivity(userId, limit);

            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent activity for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while retrieving recent activity");
        }
    }

    #region Private Helper Methods

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }

    private async Task<bool> IsCurrentUserAdmin()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return false;

        var user = await _userManager.FindByIdAsync(userId);
        return user != null && await _userManager.IsInRoleAsync(user, "Admin");
    }

    private async Task<UserSummary> GetUserSummary(PortalUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        
        return new UserSummary
        {
            UserId = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = user.Email ?? "",
            Roles = roles.ToList(),
            LastLoginDate = DateTime.UtcNow, // This would be tracked in a real application
            MemberSince = DateTime.UtcNow // This would be the actual registration date
        };
    }

    private async Task<WalletSummary> GetWalletSummary(string userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        var transactions = await _context.WalletTransactions
            .Where(t => t.UserId == userId)
            .ToListAsync();

        var totalDeposited = transactions
            .Where(t => t.Type == WalletTransactionType.Deposit)
            .Sum(t => t.Amount);

        var totalWithdrawn = transactions
            .Where(t => t.Type == WalletTransactionType.Withdrawal)
            .Sum(t => t.Amount);

        var totalSpent = transactions
            .Where(t => t.Type == WalletTransactionType.PaymentMade)
            .Sum(t => t.Amount);

        var totalEarned = transactions
            .Where(t => t.Type == WalletTransactionType.PaymentReceived)
            .Sum(t => t.Amount);

        return new WalletSummary
        {
            WalletBalance = user?.WalletBalance ?? 0,
            BlockedAmount = user?.BlockedAmount ?? 0,
            TotalTransactions = transactions.Count,
            TotalDeposited = totalDeposited,
            TotalWithdrawn = totalWithdrawn,
            TotalSpent = totalSpent,
            TotalEarned = totalEarned
        };
    }

    private async Task<AuctionsSummary> GetAuctionsSummary(string userId)
    {
        var userAuctions = await _context.Auctions
            .Where(a => a.SellerId == userId)
            .ToListAsync();

        var userBids = await _context.BidHistories
            .Include(bh => bh.Auction)
            .Where(bh => bh.BidderId == userId)
            .ToListAsync();

        var auctionsWon = await _context.Auctions
            .Where(a => a.CurrentHighestBidderId == userId && 
                       (a.Status == AuctionStatus.Expired || a.IsExpired()))
            .CountAsync();

        var activeBidsAsHighest = await _context.Auctions
            .Where(a => a.CurrentHighestBidderId == userId && 
                       a.Status == AuctionStatus.Live && !a.IsExpired())
            .CountAsync();

        var totalEarned = await _context.WalletTransactions
            .Where(t => t.UserId == userId && t.Type == WalletTransactionType.PaymentReceived)
            .SumAsync(t => t.Amount);

        var totalSpent = await _context.WalletTransactions
            .Where(t => t.UserId == userId && t.Type == WalletTransactionType.PaymentMade)
            .SumAsync(t => t.Amount);

        return new AuctionsSummary
        {
            TotalAuctionsCreated = userAuctions.Count,
            ActiveAuctionsAsseller = userAuctions.Count(a => a.Status == AuctionStatus.Live && !a.IsExpired()),
            CompletedAuctionsAsSeller = userAuctions.Count(a => a.Status == AuctionStatus.Expired || a.IsExpired()),
            TotalBidsPlaced = userBids.Count,
            ActiveBidsAsHighest = activeBidsAsHighest,
            AuctionsWon = auctionsWon,
            TotalEarnedFromSales = totalEarned,
            TotalSpentOnPurchases = totalSpent
        };
    }

    private async Task<AssetsSummary> GetAssetsSummary(string userId)
    {
        var userAssets = await _context.Assets
            .Where(a => a.OwnerId == userId)
            .ToListAsync();

        var totalRetailValue = userAssets.Sum(a => a.RetailValue);
        var averageRetailValue = userAssets.Count > 0 ? totalRetailValue / (decimal)userAssets.Count : 0;

        return new AssetsSummary
        {
            TotalAssetsOwned = userAssets.Count,
            AssetsInDraft = userAssets.Count(a => a.Status == AssetStatus.Draft),
            AssetsOpenToAuction = userAssets.Count(a => a.Status == AssetStatus.OpenToAuction),
            AssetsInActiveAuction = userAssets.Count(a => a.Status == AssetStatus.ClosedForAuction),
            TotalRetailValue = totalRetailValue,
            AverageRetailValue = averageRetailValue
        };
    }

    private async Task<List<ActiveAuctionSummary>> GetActiveAuctionsSummary(string userId, int limit = 5)
    {
        var auctions = await _context.Auctions
            .Include(a => a.Asset)
            .Include(a => a.Seller)
            .Include(a => a.CurrentHighestBidder)
            .Include(a => a.BidHistories)
            .Where(a => a.Status == AuctionStatus.Live && !a.IsExpired())
            .OrderByDescending(a => a.CurrentHighestBidderId == userId ? 1 : 0)
            .ThenBy(a => a.StartDate.AddMinutes(a.TotalMinutesToExpiry))
            .Take(limit)
            .ToListAsync();

        return auctions.Select(auction => new ActiveAuctionSummary
        {
            Id = auction.Id,
            AssetTitle = auction.Asset?.Title ?? "Unknown Asset",
            AssetDescription = auction.Asset?.Description ?? "",
            AssetRetailValue = auction.Asset?.RetailValue ?? 0,
            SellerName = $"{auction.Seller?.FirstName} {auction.Seller?.LastName}".Trim(),
            ReservedPrice = auction.ReservedPrice,
            CurrentHighestBid = auction.CurrentHighestBid,
            CurrentHighestBidderName = auction.CurrentHighestBidder != null 
                ? $"{auction.CurrentHighestBidder.FirstName} {auction.CurrentHighestBidder.LastName}".Trim() 
                : null,
            MinimumBidIncrement = auction.MinimumBidIncrement,
            StartDate = auction.StartDate,
            ExpiryDate = auction.StartDate.AddMinutes(auction.TotalMinutesToExpiry),
            RemainingTimeInMinutes = auction.GetRemainingTimeInMinutes(),
            Status = auction.Status.ToString(),
            UserIsHighestBidder = auction.CurrentHighestBidderId == userId,
            UserIsSeller = auction.SellerId == userId,
            CanBid = !auction.IsExpired() && auction.SellerId != userId && auction.Status == AuctionStatus.Live,
            TotalBids = auction.BidHistories?.Count ?? 0
        }).ToList();
    }

    private async Task<List<UserBidSummary>> GetUserActiveBids(string userId, int limit = 10)
    {
        var userBids = await _context.BidHistories
            .Include(bh => bh.Auction)
            .ThenInclude(a => a.Asset)
            .Include(bh => bh.Auction.Seller)
            .Where(bh => bh.BidderId == userId && 
                        bh.Auction.Status == AuctionStatus.Live && 
                        !bh.Auction.IsExpired())
            .GroupBy(bh => bh.AuctionId)
            .Select(g => g.OrderByDescending(bh => bh.BidDate).First())
            .OrderByDescending(bh => bh.BidDate)
            .Take(limit)
            .ToListAsync();

        return userBids.Select(bid => new UserBidSummary
        {
            AuctionId = bid.AuctionId,
            AssetTitle = bid.Auction.Asset?.Title ?? "Unknown Asset",
            UserBidAmount = bid.BidAmount,
            CurrentHighestBid = bid.Auction.CurrentHighestBid,
            IsCurrentHighest = bid.Auction.CurrentHighestBidderId == userId,
            BidDate = bid.BidDate,
            AuctionExpiry = bid.Auction.StartDate.AddMinutes(bid.Auction.TotalMinutesToExpiry),
            RemainingTimeInMinutes = bid.Auction.GetRemainingTimeInMinutes(),
            AuctionStatus = bid.Auction.Status.ToString(),
            IsExpired = bid.Auction.IsExpired(),
            NextCallPrice = bid.Auction.CurrentHighestBid + bid.Auction.MinimumBidIncrement,
            SellerName = $"{bid.Auction.Seller?.FirstName} {bid.Auction.Seller?.LastName}".Trim()
        }).ToList();
    }

    private async Task<List<RecentActivityItem>> GetRecentActivity(string userId, int limit = 20)
    {
        var activities = new List<RecentActivityItem>();

        // Get recent wallet transactions
        var walletTransactions = await _context.WalletTransactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            .Take(limit / 2)
            .ToListAsync();

        foreach (var transaction in walletTransactions)
        {
            activities.Add(new RecentActivityItem
            {
                Type = transaction.Type.ToString(),
                Description = transaction.Description,
                Timestamp = transaction.TransactionDate,
                Amount = transaction.Amount,
                Icon = GetActivityIcon(transaction.Type.ToString()),
                Color = GetActivityColor(transaction.Type.ToString())
            });
        }

        // Get recent bid activities
        var recentBids = await _context.BidHistories
            .Include(bh => bh.Auction)
            .ThenInclude(a => a.Asset)
            .Where(bh => bh.BidderId == userId)
            .OrderByDescending(bh => bh.BidDate)
            .Take(limit / 2)
            .ToListAsync();

        foreach (var bid in recentBids)
        {
            activities.Add(new RecentActivityItem
            {
                Type = "BidPlaced",
                Description = $"Placed bid of ${bid.BidAmount:F2} on {bid.Auction.Asset?.Title}",
                Timestamp = bid.BidDate,
                Amount = bid.BidAmount,
                AssetTitle = bid.Auction.Asset?.Title,
                AuctionId = bid.AuctionId,
                Icon = "💰",
                Color = "blue"
            });
        }

        // Get recent auction activities
        var recentAuctions = await _context.Auctions
            .Include(a => a.Asset)
            .Where(a => a.SellerId == userId)
            .OrderByDescending(a => a.StartDate)
            .Take(limit / 4)
            .ToListAsync();

        foreach (var auction in recentAuctions)
        {
            activities.Add(new RecentActivityItem
            {
                Type = "AuctionCreated",
                Description = $"Created auction for {auction.Asset?.Title}",
                Timestamp = auction.StartDate,
                AssetTitle = auction.Asset?.Title,
                AuctionId = auction.Id,
                Icon = "🔨",
                Color = "green"
            });
        }

        return activities
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .ToList();
    }

    private async Task<PlatformStats> GetPlatformStats()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalActiveAuctions = await _context.Auctions
            .CountAsync(a => a.Status == AuctionStatus.Live && !a.IsExpired());
        var totalCompletedAuctions = await _context.Auctions
            .CountAsync(a => a.Status == AuctionStatus.Expired || a.IsExpired());
        var totalAssets = await _context.Assets.CountAsync();
        var totalBidsPlaced = await _context.BidHistories.CountAsync();

        var totalTransactionVolume = await _context.WalletTransactions
            .Where(t => t.Type == WalletTransactionType.PaymentMade)
            .SumAsync(t => t.Amount);

        var averageAuctionValue = totalCompletedAuctions > 0 
            ? await _context.Auctions
                .Where(a => a.Status == AuctionStatus.Expired && a.CurrentHighestBid > 0)
                .AverageAsync(a => a.CurrentHighestBid)
            : 0;

        var auctionsWithBids = await _context.Auctions
            .CountAsync(a => a.CurrentHighestBid > 0);
        var totalAuctions = await _context.Auctions.CountAsync();
        var auctionSuccessRate = totalAuctions > 0 ? (double)auctionsWithBids / totalAuctions * 100 : 0;

        return new PlatformStats
        {
            TotalUsers = totalUsers,
            TotalActiveAuctions = totalActiveAuctions,
            TotalCompletedAuctions = totalCompletedAuctions,
            TotalAssets = totalAssets,
            TotalTransactionVolume = totalTransactionVolume,
            AverageAuctionValue = averageAuctionValue,
            TotalBidsPlaced = totalBidsPlaced,
            AuctionSuccessRate = auctionSuccessRate
        };
    }

    private async Task<UserStats> GetUserStats(string userId)
    {
        var userTransactionVolume = await _context.WalletTransactions
            .Where(t => t.UserId == userId && 
                       (t.Type == WalletTransactionType.PaymentMade || t.Type == WalletTransactionType.PaymentReceived))
            .SumAsync(t => t.Amount);

        var userAuctionsCreated = await _context.Auctions
            .CountAsync(a => a.SellerId == userId);

        var userBidsPlaced = await _context.BidHistories
            .CountAsync(bh => bh.BidderId == userId);

        var userAuctionsWon = await _context.Auctions
            .CountAsync(a => a.CurrentHighestBidderId == userId && 
                           (a.Status == AuctionStatus.Expired || a.IsExpired()));

        var userAuctionsWithBids = await _context.Auctions
            .CountAsync(a => a.SellerId == userId && a.CurrentHighestBid > 0);
        var userSuccessRate = userAuctionsCreated > 0 ? (double)userAuctionsWithBids / userAuctionsCreated * 100 : 0;

        // Calculate user rank based on transaction volume
        var usersWithHigherVolume = await _context.WalletTransactions
            .Where(t => t.Type == WalletTransactionType.PaymentMade || t.Type == WalletTransactionType.PaymentReceived)
            .GroupBy(t => t.UserId)
            .Select(g => new { UserId = g.Key, Volume = g.Sum(t => t.Amount) })
            .CountAsync(u => u.Volume > userTransactionVolume);

        return new UserStats
        {
            UserRank = usersWithHigherVolume + 1,
            UserTransactionVolume = userTransactionVolume,
            UserAuctionsCreated = userAuctionsCreated,
            UserBidsPlaced = userBidsPlaced,
            UserAuctionsWon = userAuctionsWon,
            UserSuccessRate = userSuccessRate
        };
    }

    private static string GetActivityIcon(string activityType)
    {
        return activityType switch
        {
            "Deposit" => "💳",
            "Withdrawal" => "💸",
            "BidBlocked" => "🔒",
            "BidReleased" => "🔓",
            "PaymentMade" => "💰",
            "PaymentReceived" => "💵",
            "BidPlaced" => "🔨",
            "AuctionCreated" => "📦",
            "AuctionWon" => "🏆",
            "AuctionExpired" => "⏰",
            _ => "📋"
        };
    }

    private static string GetActivityColor(string activityType)
    {
        return activityType switch
        {
            "Deposit" => "green",
            "Withdrawal" => "orange",
            "BidBlocked" => "red",
            "BidReleased" => "blue",
            "PaymentMade" => "red",
            "PaymentReceived" => "green",
            "BidPlaced" => "blue",
            "AuctionCreated" => "purple",
            "AuctionWon" => "gold",
            "AuctionExpired" => "gray",
            _ => "black"
        };
    }

    #endregion
} 