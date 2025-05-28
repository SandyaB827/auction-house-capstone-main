namespace TheAuctionHouse.Domain.Entities;

public class BidHistory
{
    public int Id { get; set; }
    public int AuctionId { get; set; }
    public string BidderId { get; set; } = string.Empty;
    public string BidderName { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public DateTime BidDate { get; set; }

    // Navigation properties
    public virtual Auction Auction { get; set; } = null!;
    public virtual PortalUser Bidder { get; set; } = null!;
}