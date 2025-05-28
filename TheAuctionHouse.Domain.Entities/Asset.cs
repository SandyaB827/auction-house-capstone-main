namespace TheAuctionHouse.Domain.Entities;

public class Asset
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty; // Foreign Key to PortalUser (Identity uses string IDs)
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RetailValue { get; set; }
    public AssetStatus Status { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual PortalUser Owner { get; set; } = null!;
    public virtual ICollection<Auction> Auctions { get; set; } = new List<Auction>();
}

public enum AssetStatus
{
    Draft,
    OpenToAuction,
    ClosedForAuction
}