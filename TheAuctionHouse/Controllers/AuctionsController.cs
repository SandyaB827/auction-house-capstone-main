using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using TheAuctionHouse.Data.EFCore.SQLite;
using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Models;

namespace TheAuctionHouse.Controllers;

/// <summary>
/// Controller for managing auctions in the auction house system
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // All endpoints require authentication
public class AuctionsController : ControllerBase
{
    private readonly AuctionHouseDbContext _context;
    private readonly UserManager<PortalUser> _userManager;
    private readonly ILogger<AuctionsController> _logger;

    public AuctionsController(
        AuctionHouseDbContext context,
        UserManager<PortalUser> userManager,
        ILogger<AuctionsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Post an auction (SRS 4.4.1)
    /// </summary>
    /// <param name="request">Auction creation request</param>
    /// <returns>Created auction details</returns>
    [HttpPost]
    [Authorize(Policy = "SellerOrAdmin")]
    public async Task<ActionResult<AuctionResponse>> PostAuction([FromBody] PostAuctionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not found");
            }

            // Validate asset exists and is owned by user
            var asset = await _context.Assets
                .Include(a => a.Owner)
                .FirstOrDefaultAsync(a => a.Id == request.AssetId);

            if (asset == null)
            {
                return NotFound("Asset not found");
            }

            var isAdmin = await IsCurrentUserAdmin();
            if (!isAdmin && asset.OwnerId != userId)
            {
                return Forbid("You can only auction your own assets");
            }

            // SRS 4.4.1.1: Asset must be in OpenToAuction status
            if (asset.Status != AssetStatus.OpenToAuction)
            {
                return BadRequest("Asset must be in 'OpenToAuction' status to be auctioned");
            }

            // Check if asset is already in an active auction
            var existingAuction = await _context.Auctions
                .FirstOrDefaultAsync(a => a.AssetId == request.AssetId && a.Status == AuctionStatus.Live);

            if (existingAuction != null)
            {
                return BadRequest("Asset is already in an active auction");
            }

            // Create auction
            var auction = new Auction
            {
                SellerId = userId,
                AssetId = request.AssetId,
                ReservedPrice = request.ReservedPrice,
                MinimumBidIncrement = request.MinimumBidIncrement,
                TotalMinutesToExpiry = request.TotalMinutesToExpiry,
                StartDate = DateTime.UtcNow,
                Status = AuctionStatus.Live,
                CurrentHighestBid = 0
            };

            _context.Auctions.Add(auction);

            // Update asset status to ClosedForAuction
            asset.Status = AssetStatus.ClosedForAuction;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Auction created with ID {AuctionId} for asset {AssetId} by user {UserId}", 
                auction.Id, request.AssetId, userId);

            return CreatedAtAction(nameof(GetAuction), new { id = auction.Id }, 
                await MapToAuctionResponse(auction, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating auction for asset {AssetId}", request.AssetId);
            return StatusCode(500, "An error occurred while creating the auction");
        }
    }

    /// <summary>
    /// Place a bid on an auction
    /// </summary>
    /// <param name="id">Auction ID</param>
    /// <param name="request">Bid request</param>
    /// <returns>Bid result</returns>
    [HttpPost("{id}/bid")]
    [Authorize(Policy = "BidderOrAdmin")]
    public async Task<ActionResult<BidResponse>> PlaceBid(int id, [FromBody] PlaceBidRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not found");
            }

            var auction = await _context.Auctions
                .Include(a => a.Asset)
                .Include(a => a.Seller)
                .Include(a => a.CurrentHighestBidder)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (auction == null)
            {
                return NotFound("Auction not found");
            }

            // Validate auction is still live
            if (auction.IsExpired())
            {
                return BadRequest("Auction has expired");
            }

            if (auction.Status != AuctionStatus.Live)
            {
                return BadRequest("Auction is not active");
            }

            // Sellers cannot bid on their own auctions
            if (auction.SellerId == userId)
            {
                return BadRequest("You cannot bid on your own auction");
            }

            // Validate bid amount
            var minimumBid = auction.CurrentHighestBid > 0 
                ? auction.CurrentHighestBid + auction.MinimumBidIncrement 
                : auction.ReservedPrice;

            if (request.BidAmount < minimumBid)
            {
                return BadRequest($"Bid amount must be at least ${minimumBid:F2}");
            }

            // Get user for wallet validation
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Check if user has sufficient available balance
            var availableBalance = user.WalletBalance - user.BlockedAmount;
            if (availableBalance < request.BidAmount)
            {
                return BadRequest($"Insufficient funds. Available balance: ${availableBalance:F2}, Required: ${request.BidAmount:F2}");
            }

            // Release previous highest bidder's blocked amount (if any)
            if (!string.IsNullOrEmpty(auction.CurrentHighestBidderId) && auction.CurrentHighestBid > 0)
            {
                await ReleaseBlockedAmount(auction.CurrentHighestBidderId, auction.CurrentHighestBid, auction.Id, 
                    "Previous bid amount released - outbid");
            }

            // Block new bid amount
            await BlockBidAmount(userId, request.BidAmount, auction.Id, 
                $"Bid amount blocked for auction #{auction.Id} - {auction.Asset?.Title}");

            // Update auction with new highest bid
            auction.CurrentHighestBid = request.BidAmount;
            auction.CurrentHighestBidderId = userId;

            // Create bid history record
            var bidHistory = new BidHistory
            {
                AuctionId = auction.Id,
                BidderId = userId,
                BidderName = $"{user.FirstName} {user.LastName}".Trim(),
                BidAmount = request.BidAmount,
                BidDate = DateTime.UtcNow
            };

            _context.BidHistories.Add(bidHistory);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Bid placed: ${Amount} by user {UserId} on auction {AuctionId}", 
                request.BidAmount, userId, auction.Id);

            var response = new BidResponse
            {
                Message = "Bid placed successfully",
                BidAmount = request.BidAmount,
                NextCallPrice = request.BidAmount + auction.MinimumBidIncrement,
                IsHighestBid = true,
                BlockedAmount = user.BlockedAmount + request.BidAmount,
                AvailableBalance = user.WalletBalance - (user.BlockedAmount + request.BidAmount),
                BidHistory = new BidHistoryResponse
                {
                    Id = bidHistory.Id,
                    BidderName = bidHistory.BidderName,
                    BidAmount = bidHistory.BidAmount,
                    BidDate = bidHistory.BidDate,
                    IsCurrentHighest = true
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error placing bid on auction {AuctionId}", id);
            return StatusCode(500, "An error occurred while placing the bid");
        }
    }

    /// <summary>
    /// Get all active auctions (public listing)
    /// </summary>
    /// <param name="sortBy">Sort by: expiry, bid, created (default: expiry)</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <returns>List of active auctions</returns>
    [HttpGet]
    [Authorize(Policy = "AuthenticatedUser")]
    public async Task<ActionResult<AuctionListResponse>> GetActiveAuctions(
        string sortBy = "expiry", int page = 1, int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Validate pagination parameters
            page = Math.Max(1, page);
            pageSize = Math.Min(100, Math.Max(1, pageSize));

            var currentTime = DateTime.UtcNow;
            
            var query = _context.Auctions
                .Include(a => a.Asset)
                .Include(a => a.Seller)
                .Include(a => a.CurrentHighestBidder)
                .Where(a => a.Status == AuctionStatus.Live && 
                           a.StartDate.AddMinutes(a.TotalMinutesToExpiry) > currentTime);

            // Apply sorting based on SRS 4.3.1 requirements
            query = sortBy.ToLower() switch
            {
                "bid" => query.OrderByDescending(a => a.CurrentHighestBidderId == userId ? 1 : 0)
                             .ThenBy(a => a.StartDate.AddMinutes(a.TotalMinutesToExpiry)),
                "created" => query.OrderByDescending(a => a.StartDate),
                _ => query.OrderByDescending(a => a.CurrentHighestBidderId == userId ? 1 : 0)
                          .ThenBy(a => a.StartDate.AddMinutes(a.TotalMinutesToExpiry)) // Default: expiry with user bids first
            };

            var totalCount = await query.CountAsync();
            var auctions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var auctionResponses = new List<AuctionResponse>();
            foreach (var auction in auctions)
            {
                auctionResponses.Add(await MapToAuctionResponse(auction, userId));
            }

            return Ok(new AuctionListResponse
            {
                Auctions = auctionResponses,
                TotalCount = totalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active auctions");
            return StatusCode(500, "An error occurred while retrieving auctions");
        }
    }

    /// <summary>
    /// Get a specific auction by ID
    /// </summary>
    /// <param name="id">Auction ID</param>
    /// <returns>Auction details</returns>
    [HttpGet("{id}")]
    [Authorize(Policy = "AuthenticatedUser")]
    public async Task<ActionResult<AuctionResponse>> GetAuction(int id)
    {
        try
        {
            var userId = GetCurrentUserId();

            var auction = await _context.Auctions
                .Include(a => a.Asset)
                .Include(a => a.Seller)
                .Include(a => a.CurrentHighestBidder)
                .Include(a => a.BidHistories)
                .ThenInclude(bh => bh.Bidder)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (auction == null)
            {
                return NotFound("Auction not found");
            }

            return Ok(await MapToAuctionResponse(auction, userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving auction {AuctionId}", id);
            return StatusCode(500, "An error occurred while retrieving the auction");
        }
    }

    /// <summary>
    /// Close an auction (manual closure by seller or admin)
    /// </summary>
    /// <param name="id">Auction ID</param>
    /// <returns>Auction closure result</returns>
    [HttpPost("{id}/close")]
    [Authorize(Policy = "SellerOrAdmin")]
    public async Task<ActionResult<AuctionCloseResponse>> CloseAuction(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = await IsCurrentUserAdmin();

            var auction = await _context.Auctions
                .Include(a => a.Asset)
                .Include(a => a.Seller)
                .Include(a => a.CurrentHighestBidder)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (auction == null)
            {
                return NotFound("Auction not found");
            }

            // Check permissions
            if (!isAdmin && auction.SellerId != userId)
            {
                return Forbid("You can only close your own auctions");
            }

            if (auction.Status != AuctionStatus.Live)
            {
                return BadRequest("Auction is not active");
            }

            return Ok(await ProcessAuctionClosure(auction));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing auction {AuctionId}", id);
            return StatusCode(500, "An error occurred while closing the auction");
        }
    }

    /// <summary>
    /// Get user's auction history (as seller)
    /// </summary>
    /// <returns>User's auctions</returns>
    [HttpGet("my-auctions")]
    [Authorize(Policy = "SellerOrAdmin")]
    public async Task<ActionResult<AuctionListResponse>> GetMyAuctions()
    {
        try
        {
            var userId = GetCurrentUserId();

            var auctions = await _context.Auctions
                .Include(a => a.Asset)
                .Include(a => a.Seller)
                .Include(a => a.CurrentHighestBidder)
                .Where(a => a.SellerId == userId)
                .OrderByDescending(a => a.StartDate)
                .ToListAsync();

            var auctionResponses = new List<AuctionResponse>();
            foreach (var auction in auctions)
            {
                auctionResponses.Add(await MapToAuctionResponse(auction, userId));
            }

            return Ok(new AuctionListResponse
            {
                Auctions = auctionResponses,
                TotalCount = auctionResponses.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user auctions for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while retrieving your auctions");
        }
    }

    /// <summary>
    /// Get user's bid history
    /// </summary>
    /// <returns>User's bids</returns>
    [HttpGet("my-bids")]
    [Authorize(Policy = "BidderOrAdmin")]
    public async Task<ActionResult<object>> GetMyBids()
    {
        try
        {
            var userId = GetCurrentUserId();

            var bids = await _context.BidHistories
                .Include(bh => bh.Auction)
                .ThenInclude(a => a.Asset)
                .Include(bh => bh.Auction.Seller)
                .Where(bh => bh.BidderId == userId)
                .OrderByDescending(bh => bh.BidDate)
                .ToListAsync();

            var bidResponses = bids.Select(bid => new
            {
                Id = bid.Id,
                AuctionId = bid.AuctionId,
                AssetTitle = bid.Auction.Asset?.Title ?? "Unknown Asset",
                BidAmount = bid.BidAmount,
                BidDate = bid.BidDate,
                IsCurrentHighest = bid.Auction.CurrentHighestBidderId == userId,
                AuctionStatus = bid.Auction.Status.ToString(),
                AuctionExpiry = bid.Auction.StartDate.AddMinutes(bid.Auction.TotalMinutesToExpiry),
                IsExpired = bid.Auction.IsExpired()
            }).ToList();

            return Ok(new { Bids = bidResponses, TotalCount = bidResponses.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user bids for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while retrieving your bids");
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

    private async Task BlockBidAmount(string userId, decimal amount, int auctionId, string description)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            user.BlockedAmount += amount;

            var transaction = new Domain.Entities.WalletTransaction
            {
                UserId = userId,
                Type = WalletTransactionType.BidBlocked,
                Amount = amount,
                Description = description,
                Status = WalletTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow,
                RelatedAuctionId = auctionId
            };

            _context.WalletTransactions.Add(transaction);
        }
    }

    private async Task ReleaseBlockedAmount(string userId, decimal amount, int auctionId, string description)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user != null)
        {
            user.BlockedAmount = Math.Max(0, user.BlockedAmount - amount);

            var transaction = new Domain.Entities.WalletTransaction
            {
                UserId = userId,
                Type = WalletTransactionType.BidReleased,
                Amount = amount,
                Description = description,
                Status = WalletTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow,
                RelatedAuctionId = auctionId
            };

            _context.WalletTransactions.Add(transaction);
        }
    }

    private async Task<AuctionCloseResponse> ProcessAuctionClosure(Auction auction)
    {
        var response = new AuctionCloseResponse();

        if (auction.CurrentHighestBid > 0 && !string.IsNullOrEmpty(auction.CurrentHighestBidderId))
        {
            // Auction has bids - process sale
            auction.Status = AuctionStatus.Expired;

            // Transfer asset ownership (SRS Business Rule 2)
            if (auction.Asset != null)
            {
                auction.Asset.OwnerId = auction.CurrentHighestBidderId;
                auction.Asset.Status = AssetStatus.OpenToAuction; // New owner can auction again
                response.AssetTransferred = true;
            }

            // Process payment
            var winner = await _context.Users.FirstOrDefaultAsync(u => u.Id == auction.CurrentHighestBidderId);
            var seller = await _context.Users.FirstOrDefaultAsync(u => u.Id == auction.SellerId);

            if (winner != null && seller != null)
            {
                // Deduct from winner's wallet (amount was already blocked)
                winner.BlockedAmount -= auction.CurrentHighestBid;
                winner.WalletBalance -= auction.CurrentHighestBid;

                // Add to seller's wallet
                seller.WalletBalance += auction.CurrentHighestBid;

                // Create payment transactions
                var paymentMadeTransaction = new Domain.Entities.WalletTransaction
                {
                    UserId = auction.CurrentHighestBidderId,
                    Type = WalletTransactionType.PaymentMade,
                    Amount = auction.CurrentHighestBid,
                    Description = $"Payment for winning auction #{auction.Id} - {auction.Asset?.Title}",
                    Status = WalletTransactionStatus.Completed,
                    TransactionDate = DateTime.UtcNow,
                    RelatedAuctionId = auction.Id,
                    RelatedAssetId = auction.AssetId
                };

                var paymentReceivedTransaction = new Domain.Entities.WalletTransaction
                {
                    UserId = auction.SellerId,
                    Type = WalletTransactionType.PaymentReceived,
                    Amount = auction.CurrentHighestBid,
                    Description = $"Payment received for auction #{auction.Id} - {auction.Asset?.Title}",
                    Status = WalletTransactionStatus.Completed,
                    TransactionDate = DateTime.UtcNow,
                    RelatedAuctionId = auction.Id,
                    RelatedAssetId = auction.AssetId
                };

                _context.WalletTransactions.AddRange(paymentMadeTransaction, paymentReceivedTransaction);
                response.PaymentProcessed = true;
            }

            response.Message = "Auction closed successfully with winning bid";
            response.WinningBid = auction.CurrentHighestBid;
            response.WinnerName = auction.CurrentHighestBidder?.FirstName + " " + auction.CurrentHighestBidder?.LastName;
        }
        else
        {
            // No bids - return asset to OpenToAuction status (SRS 4.1.5)
            auction.Status = AuctionStatus.ExpiredWithoutBids;
            if (auction.Asset != null)
            {
                auction.Asset.Status = AssetStatus.OpenToAuction;
            }

            response.Message = "Auction closed without bids";
            response.AssetTransferred = false;
            response.PaymentProcessed = false;
        }

        response.Status = auction.Status.ToString();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Auction {AuctionId} closed with status {Status}", auction.Id, auction.Status);

        return response;
    }

    private async Task<AuctionResponse> MapToAuctionResponse(Auction auction, string currentUserId)
    {
        var isAdmin = await IsCurrentUserAdmin();
        var isSeller = auction.SellerId == currentUserId;
        var isCurrentHighestBidder = auction.CurrentHighestBidderId == currentUserId;

        var bidHistory = auction.BidHistories?
            .OrderByDescending(bh => bh.BidDate)
            .Select(bh => new BidHistoryResponse
            {
                Id = bh.Id,
                BidderName = bh.BidderName,
                BidAmount = bh.BidAmount,
                BidDate = bh.BidDate,
                IsCurrentHighest = bh.BidderId == auction.CurrentHighestBidderId
            }).ToList() ?? new List<BidHistoryResponse>();

        return new AuctionResponse
        {
            Id = auction.Id,
            SellerId = auction.SellerId,
            SellerName = $"{auction.Seller?.FirstName} {auction.Seller?.LastName}".Trim(),
            AssetId = auction.AssetId,
            AssetTitle = auction.Asset?.Title ?? "Unknown Asset",
            AssetDescription = auction.Asset?.Description ?? "",
            AssetRetailValue = auction.Asset?.RetailValue ?? 0,
            ReservedPrice = auction.ReservedPrice,
            CurrentHighestBid = auction.CurrentHighestBid,
            CurrentHighestBidderId = auction.CurrentHighestBidderId,
            CurrentHighestBidderName = auction.CurrentHighestBidder != null 
                ? $"{auction.CurrentHighestBidder.FirstName} {auction.CurrentHighestBidder.LastName}".Trim() 
                : null,
            MinimumBidIncrement = auction.MinimumBidIncrement,
            StartDate = auction.StartDate,
            TotalMinutesToExpiry = auction.TotalMinutesToExpiry,
            RemainingTimeInMinutes = auction.GetRemainingTimeInMinutes(),
            Status = auction.Status.ToString(),
            IsExpired = auction.IsExpired(),
            CanBid = !auction.IsExpired() && !isSeller && auction.Status == AuctionStatus.Live,
            CanClose = (isSeller || isAdmin) && auction.Status == AuctionStatus.Live,
            BidHistory = bidHistory
        };
    }

    #endregion
} 