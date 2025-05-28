using System.ComponentModel.DataAnnotations;

namespace TheAuctionHouse.Models;

public class DepositRequest
{
    [Required(ErrorMessage = "Amount is required")]
    [Range(1, 999999, ErrorMessage = "Amount must be between $1 and $999,999")]
    public decimal Amount { get; set; }
}

public class WithdrawRequest
{
    [Required(ErrorMessage = "Amount is required")]
    [Range(1, 999999, ErrorMessage = "Amount must be between $1 and $999,999")]
    public decimal Amount { get; set; }
}

public class WalletResponse
{
    public decimal WalletBalance { get; set; }
    public decimal BlockedAmount { get; set; }
    public decimal AvailableBalance => WalletBalance - BlockedAmount;
    public List<BlockedAmountDetail> BlockedAmounts { get; set; } = new();
    public List<WalletTransaction> RecentTransactions { get; set; } = new();
}

public class BlockedAmountDetail
{
    public int AuctionId { get; set; }
    public string AssetTitle { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public DateTime BidDate { get; set; }
    public string AuctionStatus { get; set; } = string.Empty;
}

public class WalletTransaction
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty; // "Deposit", "Withdrawal", "Bid_Blocked", "Bid_Released", "Payment_Received", "Payment_Made"
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Completed", "Pending", "Failed"
}

public class WalletTransactionResponse
{
    public string Message { get; set; } = string.Empty;
    public decimal NewBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public WalletTransaction Transaction { get; set; } = null!;
} 