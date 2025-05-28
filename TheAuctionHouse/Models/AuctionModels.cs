using System.ComponentModel.DataAnnotations;
using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Models;

public class PostAuctionRequest
{
    [Required(ErrorMessage = "Asset ID is required")]
    public int AssetId { get; set; }

    [Required(ErrorMessage = "Reserve price is required")]
    [Range(1, 9999, ErrorMessage = "Reserve price must be between $1 and $9,999")]
    public int ReservedPrice { get; set; }

    [Required(ErrorMessage = "Incremental value is required")]
    [Range(1, 999, ErrorMessage = "Incremental value must be between $1 and $999")]
    public int MinimumBidIncrement { get; set; }

    [Required(ErrorMessage = "Expiration time is required")]
    [Range(1, 10080, ErrorMessage = "Expiration time must be between 1 and 10,080 minutes (7 days)")]
    public int TotalMinutesToExpiry { get; set; }
}

public class PlaceBidRequest
{
    [Required(ErrorMessage = "Bid amount is required")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Bid amount must be positive")]
    public decimal BidAmount { get; set; }
}

public class AuctionResponse
{
    public int Id { get; set; }
    public string SellerId { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public int AssetId { get; set; }
    public string AssetTitle { get; set; } = string.Empty;
    public string AssetDescription { get; set; } = string.Empty;
    public int AssetRetailValue { get; set; }
    public int ReservedPrice { get; set; }
    public decimal CurrentHighestBid { get; set; }
    public string? CurrentHighestBidderId { get; set; }
    public string? CurrentHighestBidderName { get; set; }
    public int MinimumBidIncrement { get; set; }
    public decimal NextCallPrice => CurrentHighestBid > 0 ? CurrentHighestBid + MinimumBidIncrement : ReservedPrice;
    public DateTime StartDate { get; set; }
    public int TotalMinutesToExpiry { get; set; }
    public DateTime ExpiryDate => StartDate.AddMinutes(TotalMinutesToExpiry);
    public int RemainingTimeInMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public bool CanBid { get; set; }
    public bool CanClose { get; set; }
    public List<BidHistoryResponse> BidHistory { get; set; } = new();
}

public class BidHistoryResponse
{
    public int Id { get; set; }
    public string BidderName { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public DateTime BidDate { get; set; }
    public bool IsCurrentHighest { get; set; }
}

public class AuctionListResponse
{
    public List<AuctionResponse> Auctions { get; set; } = new();
    public int TotalCount { get; set; }
}

public class BidResponse
{
    public string Message { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public decimal NextCallPrice { get; set; }
    public bool IsHighestBid { get; set; }
    public decimal BlockedAmount { get; set; }
    public decimal AvailableBalance { get; set; }
    public BidHistoryResponse BidHistory { get; set; } = null!;
}

public class AuctionCloseResponse
{
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? WinningBid { get; set; }
    public string? WinnerName { get; set; }
    public bool AssetTransferred { get; set; }
    public bool PaymentProcessed { get; set; }
} 