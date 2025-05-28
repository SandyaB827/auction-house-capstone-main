using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TheAuctionHouse.Common.ErrorHandling;
using TheAuctionHouse.Common.Validation;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.ServiceContracts;

namespace TheAuctionHouse.Domain.Services;

public class AssetService : IAssetService
{
    private readonly IAppUnitOfWork _appUnitOfWork;
    
    public AssetService(IAppUnitOfWork appUnitOfWork)
    {
        _appUnitOfWork = appUnitOfWork;
    }

    public async Task<Result<bool>> CreateAssetAsync(AssetInformationUpdateRequest createAssetRequest)
    {
        try
        {
            // Validate request
            Error validationError = Error.ValidationFailures();
            if (!ValidationHelper.Validate(createAssetRequest, validationError))
            {
                return validationError;
            }

            // Process the title according to requirements
            string processedTitle = ProcessTitle(createAssetRequest.Title);
            if (processedTitle.Length < 10 || processedTitle.Length > 150)
            {
                return Error.BadRequest("Title should be between 10 and 150 characters.");
            }
            
            // Validate description
            if (createAssetRequest.Description.Length < 10 || createAssetRequest.Description.Length > 1000)
            {
                return Error.BadRequest("Description should be between 10 and 1000 characters.");
            }
            
            // Validate retail value
            if (createAssetRequest.RetailValue <= 0)
            {
                return Error.BadRequest("Retail Value should be a positive integer.");
            }

            // Create new asset
            var newAsset = new Asset
            {
                UserId = createAssetRequest.UserId,
                Title = processedTitle,
                Description = createAssetRequest.Description,
                RetailValue = createAssetRequest.RetailValue,
                Status = AssetStatus.Draft
            };

            await _appUnitOfWork.AssetRepository.AddAsync(newAsset);
            await _appUnitOfWork.CommitAsync();

            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }

    public async Task<Result<bool>> UpdateAssetAsync(AssetInformationUpdateRequest updateAssetRequest)
    {
        try
        {
            // Validate request
            Error validationError = Error.ValidationFailures();
            if (!ValidationHelper.Validate(updateAssetRequest, validationError))
            {
                return validationError;
            }
            
            // Get asset
            var asset = await _appUnitOfWork.AssetRepository.GetByIdAsync(updateAssetRequest.AssetId);
            if (asset == null)
            {
                return Error.NotFound("Asset not found.");
            }
            
            // Verify the asset belongs to the user
            if (asset.UserId != updateAssetRequest.UserId)
            {
                return Error.BadRequest("You can only update your own assets.");
            }
            
            // Check if asset is in Draft status
            if (asset.Status != AssetStatus.Draft)
            {
                return Error.BadRequest("Only assets in Draft status can be updated.");
            }

            // Process the title according to requirements
            string processedTitle = ProcessTitle(updateAssetRequest.Title);
            if (processedTitle.Length < 10 || processedTitle.Length > 150)
            {
                return Error.BadRequest("Title should be between 10 and 150 characters.");
            }
            
            // Validate description
            if (updateAssetRequest.Description.Length < 10 || updateAssetRequest.Description.Length > 1000)
            {
                return Error.BadRequest("Description should be between 10 and 1000 characters.");
            }
            
            // Validate retail value
            if (updateAssetRequest.RetailValue <= 0)
            {
                return Error.BadRequest("Retail Value should be a positive integer.");
            }

            // Update asset
            asset.Title = processedTitle;
            asset.Description = updateAssetRequest.Description;
            asset.RetailValue = updateAssetRequest.RetailValue;

            await _appUnitOfWork.AssetRepository.UpdateAsync(asset);
            await _appUnitOfWork.CommitAsync();

            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }

    public async Task<Result<bool>> DeleteAssetAsync(int assetId)
    {
        try
        {
            // Get asset
            var asset = await _appUnitOfWork.AssetRepository.GetByIdAsync(assetId);
            if (asset == null)
            {
                return Error.NotFound("Asset not found.");
            }
            
            // Check if asset is in Draft or Open status
            if (asset.Status != AssetStatus.Draft && asset.Status != AssetStatus.OpenToAuction)
            {
                return Error.BadRequest("Only assets in Draft or Open status can be deleted.");
            }

            // Delete asset
            await _appUnitOfWork.AssetRepository.RemoveAsync(asset);
            await _appUnitOfWork.CommitAsync();

            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }

    public async Task<Result<AssetResponse>> GetAssetByIdAsync(int assetId)
    {
        try
        {
            // Get asset
            var asset = await _appUnitOfWork.AssetRepository.GetByIdAsync(assetId);
            if (asset == null)
            {
                return Error.NotFound("Asset not found.");
            }
            
            // Map to response
            var response = new AssetResponse
            {
                Id = asset.Id,
                UserId = asset.UserId,
                Title = asset.Title,
                Description = asset.Description,
                RetailValue = asset.RetailValue,
                Status = asset.Status.ToString()
            };

            return response;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }

    public async Task<Result<List<AssetResponse>>> GetAllAssetsByUserIdAsync(int userId)
    {
        try
        {
            // Get all assets for the user
            var assets = await _appUnitOfWork.AssetRepository.GetAssetsByUserIdAsync(userId);
            
            // Map to response
            var responseList = new List<AssetResponse>();
            foreach (var asset in assets)
            {
                responseList.Add(new AssetResponse
                {
                    Id = asset.Id,
                    UserId = asset.UserId,
                    Title = asset.Title,
                    Description = asset.Description,
                    RetailValue = asset.RetailValue,
                    Status = asset.Status.ToString()
                });
            }
            
            return responseList;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }
    
    // Change asset status from Draft to OpenToAuction
    public async Task<Result<bool>> OpenAssetToAuctionAsync(int assetId, int userId)
    {
        try
        {
            // Get asset
            var asset = await _appUnitOfWork.AssetRepository.GetByIdAsync(assetId);
            if (asset == null)
            {
                return Error.NotFound("Asset not found.");
            }
            
            // Verify the asset belongs to the user
            if (asset.UserId != userId)
            {
                return Error.BadRequest("You can only modify your own assets.");
            }
            
            // Check if asset is in Draft status
            if (asset.Status != AssetStatus.Draft)
            {
                return Error.BadRequest("Only assets in Draft status can be opened for auction.");
            }
            
            // Update asset status
            asset.Status = AssetStatus.OpenToAuction;
            
            await _appUnitOfWork.AssetRepository.UpdateAsync(asset);
            await _appUnitOfWork.CommitAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }
    
    // Change asset status from Active to Open (for when auction expires without bids)
    public async Task<Result<bool>> RevertAssetToOpenAsync(int assetId)
    {
        try
        {
            // Get asset
            var asset = await _appUnitOfWork.AssetRepository.GetByIdAsync(assetId);
            if (asset == null)
            {
                return Error.NotFound("Asset not found.");
            }
            
            // Check if asset is in active status
            if (asset.Status != AssetStatus.ClosedForAuction)
            {
                return Error.BadRequest("Only assets in closed status can be reverted to open.");
            }
            
            // Update asset status
            asset.Status = AssetStatus.OpenToAuction;
            
            await _appUnitOfWork.AssetRepository.UpdateAsync(asset);
            await _appUnitOfWork.CommitAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }
    
    // Change asset ownership after successful auction
    public async Task<Result<bool>> ChangeAssetOwnershipAsync(int assetId, int newOwnerId)
    {
        try
        {
            // Get asset
            var asset = await _appUnitOfWork.AssetRepository.GetByIdAsync(assetId);
            if (asset == null)
            {
                return Error.NotFound("Asset not found.");
            }
            
            // Check if asset is in closed status
            if (asset.Status != AssetStatus.ClosedForAuction)
            {
                return Error.BadRequest("Only assets in closed auction status can change ownership.");
            }
            
            // Update asset ownership and status
            asset.UserId = newOwnerId;
            asset.Status = AssetStatus.OpenToAuction;
            
            await _appUnitOfWork.AssetRepository.UpdateAsync(asset);
            await _appUnitOfWork.CommitAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            return Error.InternalServerError(ex.Message);
        }
    }
    
    private string ProcessTitle(string title)
    {
        // Replace multiple consecutive spaces with a single space and trim edges
        string processed = title;
        while (processed.Contains("  "))
        {
            processed = processed.Replace("  ", " ");
        }
        processed = processed.Trim();
        
        // Remove special characters
        processed = new string(processed.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray());
        
        return processed;
    }
}
