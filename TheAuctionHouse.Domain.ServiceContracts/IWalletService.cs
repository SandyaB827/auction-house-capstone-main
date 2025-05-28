using TheAuctionHouse.Common.ErrorHandling;

namespace TheAuctionHouse.Domain.ServiceContracts;

public interface IWalletService
{
    Result<bool> DepositAsync(WalletTransactionRequest walletTransactionRequest);
    Result<bool> WithDrawalAsync(WalletTransactionRequest walletTransactionRequest);
    Result<WalletBalenceResponse> GetWalletBalenceAsync(int userId);
}
