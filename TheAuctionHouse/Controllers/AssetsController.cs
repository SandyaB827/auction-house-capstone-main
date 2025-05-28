using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using TheAuctionHouse.Data.EFCore.SQLite;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Models;

namespace TheAuctionHouse.Controllers;

/// <summary>
/// Controller for managing assets in the auction house system
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // All endpoints require authentication
public class AssetsController : ControllerBase
{
    private readonly AuctionHouseDbContext _context;
    private readonly UserManager<PortalUser> _userManager;
    private readonly ILogger<AssetsController> _logger;

    public AssetsController(
        AuctionHouseDbContext context,
        UserManager<PortalUser> userManager,
        ILogger<AssetsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Create a new asset (SRS 4.1.1)
    /// </summary>
    /// <param name="request">Asset creation request</param>
    /// <returns>Created asset details</returns>
    [HttpPost]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult<AssetResponse>> CreateAsset([FromBody] CreateAssetRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not found");
            }

            // Clean and validate title (SRS requirement: trim spaces, no special chars)
            var cleanedTitle = CleanTitle(request.Title);
            if (string.IsNullOrEmpty(cleanedTitle))
            {
                return BadRequest("Title contains invalid characters or is empty after cleaning");
            }

            var asset = new Asset
            {
                OwnerId = userId,
                Title = cleanedTitle,
                Description = request.Description.Trim(),
                RetailValue = request.RetailValue,
                Status = AssetStatus.Draft, // Default status as per SRS
                CreatedDate = DateTime.UtcNow
            };

            _context.Assets.Add(asset);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Asset created with ID {AssetId} by user {UserId}", asset.Id, userId);

            return CreatedAtAction(nameof(GetAsset), new { id = asset.Id }, await MapToAssetResponse(asset, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating asset");
            return StatusCode(500, "An error occurred while creating the asset");
        }
    }

    /// <summary>
    /// Update an existing asset (SRS 4.1.2)
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <param name="request">Asset update request</param>
    /// <returns>Updated asset details</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult<AssetResponse>> UpdateAsset(int id, [FromBody] UpdateAssetRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = await IsCurrentUserAdmin();

            var asset = await _context.Assets
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
            {
                return NotFound("Asset not found");
            }

            // Check ownership or admin rights
            if (!isAdmin && asset.OwnerId != userId)
            {
                return Forbid("You can only update your own assets");
            }

            // SRS 4.1.2.1: Only Draft status assets can be updated
            if (asset.Status != AssetStatus.Draft)
            {
                return BadRequest("Only assets in Draft status can be updated");
            }

            // Clean and validate title
            var cleanedTitle = CleanTitle(request.Title);
            if (string.IsNullOrEmpty(cleanedTitle))
            {
                return BadRequest("Title contains invalid characters or is empty after cleaning");
            }

            asset.Title = cleanedTitle;
            asset.Description = request.Description.Trim();
            asset.RetailValue = request.RetailValue;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Asset {AssetId} updated by user {UserId}", id, userId);

            return Ok(await MapToAssetResponse(asset, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset {AssetId}", id);
            return StatusCode(500, "An error occurred while updating the asset");
        }
    }

    /// <summary>
    /// Change asset status (SRS 4.1.3)
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <param name="request">Status change request</param>
    /// <returns>Updated asset details</returns>
    [HttpPatch("{id}/status")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult<AssetResponse>> ChangeAssetStatus(int id, [FromBody] ChangeAssetStatusRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = await IsCurrentUserAdmin();

            var asset = await _context.Assets
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
            {
                return NotFound("Asset not found");
            }

            // Check ownership or admin rights
            if (!isAdmin && asset.OwnerId != userId)
            {
                return Forbid("You can only change status of your own assets");
            }

            // Validate status transitions based on SRS business rules
            if (!IsValidStatusTransition(asset.Status, request.Status))
            {
                return BadRequest($"Invalid status transition from {asset.Status} to {request.Status}");
            }

            asset.Status = request.Status;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Asset {AssetId} status changed to {Status} by user {UserId}", id, request.Status, userId);

            return Ok(await MapToAssetResponse(asset, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing asset status for asset {AssetId}", id);
            return StatusCode(500, "An error occurred while changing asset status");
        }
    }

    /// <summary>
    /// Delete an asset (SRS 4.1.7)
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <returns>Success message</returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult> DeleteAsset(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = await IsCurrentUserAdmin();

            var asset = await _context.Assets
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
            {
                return NotFound("Asset not found");
            }

            // Check ownership or admin rights
            if (!isAdmin && asset.OwnerId != userId)
            {
                return Forbid("You can only delete your own assets");
            }

            // SRS 4.1.7.1: Only Open or Draft status assets can be deleted
            if (asset.Status != AssetStatus.Draft && asset.Status != AssetStatus.OpenToAuction)
            {
                return BadRequest("Only assets in Draft or OpenToAuction status can be deleted");
            }

            _context.Assets.Remove(asset);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Asset {AssetId} deleted by user {UserId}", id, userId);

            return Ok(new { message = "Asset deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting asset {AssetId}", id);
            return StatusCode(500, "An error occurred while deleting the asset");
        }
    }

    /// <summary>
    /// Get user's assets list (SRS 4.1.8)
    /// </summary>
    /// <returns>List of user's assets</returns>
    [HttpGet]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult<AssetListResponse>> GetUserAssets()
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = await IsCurrentUserAdmin();

            IQueryable<Asset> query = _context.Assets.Include(a => a.Owner);

            // Admins can see all assets, users only see their own
            if (!isAdmin)
            {
                query = query.Where(a => a.OwnerId == userId);
            }

            var assets = await query
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            var assetResponses = new List<AssetResponse>();
            foreach (var asset in assets)
            {
                assetResponses.Add(await MapToAssetResponse(asset, userId));
            }

            return Ok(new AssetListResponse
            {
                Assets = assetResponses,
                TotalCount = assetResponses.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user assets");
            return StatusCode(500, "An error occurred while retrieving assets");
        }
    }

    /// <summary>
    /// Get a specific asset by ID
    /// </summary>
    /// <param name="id">Asset ID</param>
    /// <returns>Asset details</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = "AuthenticatedUser")]
    public async Task<ActionResult<AssetResponse>> GetAsset(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = await IsCurrentUserAdmin();

            var asset = await _context.Assets
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
            {
                return NotFound("Asset not found");
            }

            // Users can only view their own assets unless they're admin
            if (!isAdmin && asset.OwnerId != userId)
            {
                return Forbid("You can only view your own assets");
            }

            return Ok(await MapToAssetResponse(asset, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset {AssetId}", id);
            return StatusCode(500, "An error occurred while retrieving the asset");
        }
    }

    /// <summary>
    /// Get all assets available for auction (public endpoint for browsing)
    /// </summary>
    /// <returns>List of assets available for auction</returns>
    [HttpGet("available")]
    [Authorize(Policy = "AuthenticatedUser")]
    public async Task<ActionResult<AssetListResponse>> GetAvailableAssets()
    {
        try
        {
            var userId = GetCurrentUserId();

            var assets = await _context.Assets
                .Include(a => a.Owner)
                .Where(a => a.Status == AssetStatus.OpenToAuction)
                .OrderByDescending(a => a.CreatedDate)
                .ToListAsync();

            var assetResponses = new List<AssetResponse>();
            foreach (var asset in assets)
            {
                assetResponses.Add(await MapToAssetResponse(asset, userId));
            }

            return Ok(new AssetListResponse
            {
                Assets = assetResponses,
                TotalCount = assetResponses.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available assets");
            return StatusCode(500, "An error occurred while retrieving available assets");
        }
    }

    #region Private Helper Methods

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }

    private async Task<bool> IsCurrentUserAdmin()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return false;

        var user = await _userManager.FindByIdAsync(userId);
        return user != null && await _userManager.IsInRoleAsync(user, "Admin");
    }

    private static string CleanTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Remove special characters and replace multiple spaces with single space
        var cleaned = Regex.Replace(title, @"[^a-zA-Z0-9\s]", "");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned.Trim();
    }

    private static bool IsValidStatusTransition(AssetStatus currentStatus, AssetStatus newStatus)
    {
        return currentStatus switch
        {
            AssetStatus.Draft => newStatus == AssetStatus.OpenToAuction,
            AssetStatus.OpenToAuction => newStatus == AssetStatus.ClosedForAuction,
            AssetStatus.ClosedForAuction => newStatus == AssetStatus.OpenToAuction,
            _ => false
        };
    }

    private async Task<AssetResponse> MapToAssetResponse(Asset asset, string currentUserId)
    {
        var isAdmin = await IsCurrentUserAdmin();
        var isOwner = asset.OwnerId == currentUserId;

        return new AssetResponse
        {
            Id = asset.Id,
            Title = asset.Title,
            Description = asset.Description,
            RetailValue = asset.RetailValue,
            Status = asset.Status.ToString(),
            OwnerName = $"{asset.Owner?.FirstName} {asset.Owner?.LastName}".Trim(),
            OwnerId = asset.OwnerId,
            CreatedDate = asset.CreatedDate,
            CanEdit = (isOwner || isAdmin) && asset.Status == AssetStatus.Draft,
            CanDelete = (isOwner || isAdmin) && (asset.Status == AssetStatus.Draft || asset.Status == AssetStatus.OpenToAuction),
            CanChangeStatus = (isOwner || isAdmin) && IsValidStatusTransitionAvailable(asset.Status)
        };
    }

    private static bool IsValidStatusTransitionAvailable(AssetStatus currentStatus)
    {
        return currentStatus switch
        {
            AssetStatus.Draft => true, // Can change to OpenToAuction
            AssetStatus.ClosedForAuction => true, // Can change back to OpenToAuction
            _ => false
        };
    }

    #endregion
} 