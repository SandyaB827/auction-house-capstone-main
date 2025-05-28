using TheAuctionHouse.Common.ErrorHandling;
public interface IAuctionService
{
    Result<bool> PostAuctionAsync(PostAuctionRequest postAuctionRequest);
    Result<bool> CheckAuctionExpiriesAsync();
    Result<AuctionResponse> GetAuctionByIdAsync(int auctionId);
    Result<List<AuctionResponse>> GetAuctionsByUserIdAsync(int userId);
    Result<List<AuctionResponse>> GetAllOpenAuctionsByUserIdAsync();
}