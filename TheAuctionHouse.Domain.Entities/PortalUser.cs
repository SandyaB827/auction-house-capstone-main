using Microsoft.AspNetCore.Identity;

namespace TheAuctionHouse.Domain.Entities;

public class PortalUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public decimal WalletBalance { get; set; }
    public decimal BlockedAmount { get; set; }

    // Navigation properties
    public virtual ICollection<Asset> AssetsOwned { get; set; } = new List<Asset>();
    public virtual ICollection<Auction> AuctionsListedAsSeller { get; set; } = new List<Auction>();
    public virtual ICollection<BidHistory> BidsPlaced { get; set; } = new List<BidHistory>();
    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
