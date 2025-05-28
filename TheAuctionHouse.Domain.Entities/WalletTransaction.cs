namespace TheAuctionHouse.Domain.Entities;

public class WalletTransaction
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty; // Foreign Key to PortalUser
    public WalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public WalletTransactionStatus Status { get; set; } = WalletTransactionStatus.Completed;
    public int? RelatedAuctionId { get; set; } // For bid-related transactions
    public int? RelatedAssetId { get; set; } // For asset-related transactions

    // Navigation properties
    public virtual PortalUser User { get; set; } = null!;
    public virtual Auction? RelatedAuction { get; set; }
    public virtual Asset? RelatedAsset { get; set; }
}

public enum WalletTransactionType
{
    Deposit,
    Withdrawal,
    BidBlocked,
    BidReleased,
    PaymentReceived,
    PaymentMade
}

public enum WalletTransactionStatus
{
    Pending,
    Completed,
    Failed,
    Cancelled
} 