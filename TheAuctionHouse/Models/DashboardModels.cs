using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Models;

public class DashboardResponse
{
    public UserSummary UserSummary { get; set; } = new();
    public WalletSummary WalletSummary { get; set; } = new();
    public AuctionsSummary AuctionsSummary { get; set; } = new();
    public AssetsSummary AssetsSummary { get; set; } = new();
    public List<ActiveAuctionSummary> ActiveAuctions { get; set; } = new();
    public List<UserBidSummary> UserActiveBids { get; set; } = new();
    public List<RecentActivityItem> RecentActivity { get; set; } = new();
}

public class UserSummary
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public DateTime LastLoginDate { get; set; }
    public DateTime MemberSince { get; set; }
}

public class WalletSummary
{
    public decimal WalletBalance { get; set; }
    public decimal BlockedAmount { get; set; }
    public decimal AvailableBalance => WalletBalance - BlockedAmount;
    public int TotalTransactions { get; set; }
    public decimal TotalDeposited { get; set; }
    public decimal TotalWithdrawn { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalEarned { get; set; }
}

public class AuctionsSummary
{
    public int TotalAuctionsCreated { get; set; }
    public int ActiveAuctionsAsseller { get; set; }
    public int CompletedAuctionsAsSeller { get; set; }
    public int TotalBidsPlaced { get; set; }
    public int ActiveBidsAsHighest { get; set; }
    public int AuctionsWon { get; set; }
    public decimal TotalEarnedFromSales { get; set; }
    public decimal TotalSpentOnPurchases { get; set; }
}

public class AssetsSummary
{
    public int TotalAssetsOwned { get; set; }
    public int AssetsInDraft { get; set; }
    public int AssetsOpenToAuction { get; set; }
    public int AssetsInActiveAuction { get; set; }
    public decimal TotalRetailValue { get; set; }
    public decimal AverageRetailValue { get; set; }
}

public class ActiveAuctionSummary
{
    public int Id { get; set; }
    public string AssetTitle { get; set; } = string.Empty;
    public string AssetDescription { get; set; } = string.Empty;
    public int AssetRetailValue { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public int ReservedPrice { get; set; }
    public decimal CurrentHighestBid { get; set; }
    public string? CurrentHighestBidderName { get; set; }
    public int MinimumBidIncrement { get; set; }
    public decimal NextCallPrice => CurrentHighestBid > 0 ? CurrentHighestBid + MinimumBidIncrement : ReservedPrice;
    public DateTime StartDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int RemainingTimeInMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool UserIsHighestBidder { get; set; }
    public bool UserIsSeller { get; set; }
    public bool CanBid { get; set; }
    public int TotalBids { get; set; }
}

public class UserBidSummary
{
    public int AuctionId { get; set; }
    public string AssetTitle { get; set; } = string.Empty;
    public decimal UserBidAmount { get; set; }
    public decimal CurrentHighestBid { get; set; }
    public bool IsCurrentHighest { get; set; }
    public DateTime BidDate { get; set; }
    public DateTime AuctionExpiry { get; set; }
    public int RemainingTimeInMinutes { get; set; }
    public string AuctionStatus { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public decimal NextCallPrice { get; set; }
    public string SellerName { get; set; } = string.Empty;
}

public class RecentActivityItem
{
    public string Type { get; set; } = string.Empty; // "BidPlaced", "AuctionCreated", "AuctionWon", "AuctionExpired", "WalletDeposit", "WalletWithdraw", "AssetCreated"
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal? Amount { get; set; }
    public string? AssetTitle { get; set; }
    public int? AuctionId { get; set; }
    public string Icon { get; set; } = string.Empty; // For UI representation
    public string Color { get; set; } = string.Empty; // For UI representation
}

public class DashboardStatsResponse
{
    public PlatformStats PlatformStats { get; set; } = new();
    public UserStats UserStats { get; set; } = new();
}

public class PlatformStats
{
    public int TotalUsers { get; set; }
    public int TotalActiveAuctions { get; set; }
    public int TotalCompletedAuctions { get; set; }
    public int TotalAssets { get; set; }
    public decimal TotalTransactionVolume { get; set; }
    public decimal AverageAuctionValue { get; set; }
    public int TotalBidsPlaced { get; set; }
    public double AuctionSuccessRate { get; set; } // Percentage of auctions that received bids
}

public class UserStats
{
    public int UserRank { get; set; } // Based on total transaction volume
    public decimal UserTransactionVolume { get; set; }
    public int UserAuctionsCreated { get; set; }
    public int UserBidsPlaced { get; set; }
    public int UserAuctionsWon { get; set; }
    public double UserSuccessRate { get; set; } // Percentage of user's auctions that received bids
} 