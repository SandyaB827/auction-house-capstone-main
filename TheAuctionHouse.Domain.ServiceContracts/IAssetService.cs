using TheAuctionHouse.Common.ErrorHandling;
public interface IAssetService
{
    Result<bool> CreateAssetAsync(AssetInformationUpdateRequest createAssetRequest);
    Result<bool> UpdateAssetAsync(AssetInformationUpdateRequest updateAssetRequest);
    Result<bool> DeleteAssetAsync(int assetId);
    Result<AssetResponse> GetAssetByIdAsync(int assetId);
    Result<List<AssetResponse>> GetAllAssetsByUserIdAsync();
}