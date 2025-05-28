namespace TheAuctionHouse.Domain.Entities;

public class Auction
{
    public int Id { get; set; }
    public string SellerId { get; set; } = string.Empty; // Foreign Key to PortalUser
    public int AssetId { get; set; }
    public int ReservedPrice { get; set; }
    public decimal CurrentHighestBid { get; set; }
    public string? CurrentHighestBidderId { get; set; } // Nullable Foreign Key to PortalUser
    public int MinimumBidIncrement { get; set; }
    public DateTime StartDate { get; set; }
    public int TotalMinutesToExpiry { get; set; }
    public AuctionStatus Status { get; set; }

    // Navigation properties
    public virtual PortalUser Seller { get; set; } = null!;
    public virtual Asset Asset { get; set; } = null!;
    public virtual PortalUser? CurrentHighestBidder { get; set; }
    public virtual ICollection<BidHistory> BidHistories { get; set; } = new List<BidHistory>();

    public int GetRemainingTimeInMinutes()
    {
        var expiryDate = StartDate.AddMinutes(TotalMinutesToExpiry);
        var remainingTime = expiryDate - DateTime.UtcNow;
        return (int)remainingTime.TotalMinutes;
    }
    public bool IsExpired()
    {
        return GetRemainingTimeInMinutes() <= 0;
    }
    public bool IsExpiredWithoutBids()
    {
        return IsExpired() && CurrentHighestBid == 0;
    }
    public bool IsLive()
    {
        return !IsExpired() && CurrentHighestBid > 0;
    }
}

public enum AuctionStatus
{
    Live,
    Expired,
    ExpiredWithoutBids
}