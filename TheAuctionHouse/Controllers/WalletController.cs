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
/// Controller for managing wallet operations in the auction house system
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize] // All endpoints require authentication
public class WalletController : ControllerBase
{
    private readonly AuctionHouseDbContext _context;
    private readonly UserManager<PortalUser> _userManager;
    private readonly ILogger<WalletController> _logger;

    public WalletController(
        AuctionHouseDbContext context,
        UserManager<PortalUser> userManager,
        ILogger<WalletController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Deposit money into wallet (SRS 4.2.1)
    /// </summary>
    /// <param name="request">Deposit request</param>
    /// <returns>Transaction details and updated wallet balance</returns>
    [HttpPost("deposit")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult<WalletTransactionResponse>> Deposit([FromBody] DepositRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not found");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Update wallet balance
            user.WalletBalance += request.Amount;

            // Create transaction record
            var transaction = new Domain.Entities.WalletTransaction
            {
                UserId = userId,
                Type = WalletTransactionType.Deposit,
                Amount = request.Amount,
                Description = $"Wallet deposit of ${request.Amount:F2}",
                Status = WalletTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow
            };

            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deposited ${Amount} to wallet", userId, request.Amount);

            var response = new WalletTransactionResponse
            {
                Message = "Deposit successful",
                NewBalance = user.WalletBalance,
                AvailableBalance = user.WalletBalance - user.BlockedAmount,
                Transaction = MapToWalletTransactionDto(transaction)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing deposit for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while processing the deposit");
        }
    }

    /// <summary>
    /// Withdraw money from wallet (SRS 4.2.2)
    /// </summary>
    /// <param name="request">Withdrawal request</param>
    /// <returns>Transaction details and updated wallet balance</returns>
    [HttpPost("withdraw")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult<WalletTransactionResponse>> Withdraw([FromBody] WithdrawRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not found");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Check available balance (wallet balance minus blocked amount)
            var availableBalance = user.WalletBalance - user.BlockedAmount;
            if (availableBalance < request.Amount)
            {
                return BadRequest($"Insufficient funds. Available balance: ${availableBalance:F2}, Requested: ${request.Amount:F2}");
            }

            // Update wallet balance
            user.WalletBalance -= request.Amount;

            // Create transaction record
            var transaction = new Domain.Entities.WalletTransaction
            {
                UserId = userId,
                Type = WalletTransactionType.Withdrawal,
                Amount = request.Amount,
                Description = $"Wallet withdrawal of ${request.Amount:F2}",
                Status = WalletTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow
            };

            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} withdrew ${Amount} from wallet", userId, request.Amount);

            var response = new WalletTransactionResponse
            {
                Message = "Withdrawal successful",
                NewBalance = user.WalletBalance,
                AvailableBalance = user.WalletBalance - user.BlockedAmount,
                Transaction = MapToWalletTransactionDto(transaction)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing withdrawal for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while processing the withdrawal");
        }
    }

    /// <summary>
    /// Get wallet dashboard with balance and transaction history (SRS 4.2.3)
    /// </summary>
    /// <returns>Wallet details including balance, blocked amounts, and recent transactions</returns>
    [HttpGet]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult<WalletResponse>> GetWalletDashboard()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not found");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Get blocked amounts from active bids
            var blockedAmounts = await GetBlockedAmountDetails(userId);

            // Get recent transactions (last 20)
            var recentTransactions = await _context.WalletTransactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .Take(20)
                .ToListAsync();

            var response = new WalletResponse
            {
                WalletBalance = user.WalletBalance,
                BlockedAmount = user.BlockedAmount,
                BlockedAmounts = blockedAmounts,
                RecentTransactions = recentTransactions.Select(MapToWalletTransactionDto).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving wallet dashboard for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while retrieving wallet information");
        }
    }

    /// <summary>
    /// Get wallet transaction history with pagination
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <returns>Paginated transaction history</returns>
    [HttpGet("transactions")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult<object>> GetTransactionHistory(int page = 1, int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("User not found");
            }

            // Validate pagination parameters
            page = Math.Max(1, page);
            pageSize = Math.Min(100, Math.Max(1, pageSize));

            var query = _context.WalletTransactions
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate);

            var totalCount = await query.CountAsync();
            var transactions = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var response = new
            {
                Transactions = transactions.Select(MapToWalletTransactionDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transaction history for user {UserId}", GetCurrentUserId());
            return StatusCode(500, "An error occurred while retrieving transaction history");
        }
    }

    /// <summary>
    /// Block amount for bid (Internal use - called by auction system)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="amount">Amount to block</param>
    /// <param name="auctionId">Related auction ID</param>
    /// <param name="description">Transaction description</param>
    /// <returns>Success status</returns>
    [HttpPost("block-amount")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult> BlockAmount([FromBody] BlockAmountRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = await IsCurrentUserAdmin();

            // Only allow users to block their own amounts or admins to block any amount
            if (!isAdmin && request.UserId != userId)
            {
                return Forbid("You can only block amounts for your own account");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Check if user has sufficient available balance
            var availableBalance = user.WalletBalance - user.BlockedAmount;
            if (availableBalance < request.Amount)
            {
                return BadRequest($"Insufficient funds. Available balance: ${availableBalance:F2}, Required: ${request.Amount:F2}");
            }

            // Block the amount
            user.BlockedAmount += request.Amount;

            // Create transaction record
            var transaction = new Domain.Entities.WalletTransaction
            {
                UserId = request.UserId,
                Type = WalletTransactionType.BidBlocked,
                Amount = request.Amount,
                Description = request.Description,
                Status = WalletTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow,
                RelatedAuctionId = request.AuctionId
            };

            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Blocked ${Amount} for user {UserId} for auction {AuctionId}", 
                request.Amount, request.UserId, request.AuctionId);

            return Ok(new { message = "Amount blocked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking amount for user {UserId}", request.UserId);
            return StatusCode(500, "An error occurred while blocking the amount");
        }
    }

    /// <summary>
    /// Release blocked amount (Internal use - called by auction system)
    /// </summary>
    /// <param name="request">Release amount request</param>
    /// <returns>Success status</returns>
    [HttpPost("release-amount")]
    [Authorize(Policy = "UserOrAdmin")]
    public async Task<ActionResult> ReleaseAmount([FromBody] ReleaseAmountRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = await IsCurrentUserAdmin();

            // Only allow users to release their own amounts or admins to release any amount
            if (!isAdmin && request.UserId != userId)
            {
                return Forbid("You can only release amounts for your own account");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Check if user has sufficient blocked amount
            if (user.BlockedAmount < request.Amount)
            {
                return BadRequest($"Insufficient blocked amount. Blocked: ${user.BlockedAmount:F2}, Requested: ${request.Amount:F2}");
            }

            // Release the amount
            user.BlockedAmount -= request.Amount;

            // Create transaction record
            var transaction = new Domain.Entities.WalletTransaction
            {
                UserId = request.UserId,
                Type = WalletTransactionType.BidReleased,
                Amount = request.Amount,
                Description = request.Description,
                Status = WalletTransactionStatus.Completed,
                TransactionDate = DateTime.UtcNow,
                RelatedAuctionId = request.AuctionId
            };

            _context.WalletTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Released ${Amount} for user {UserId} for auction {AuctionId}", 
                request.Amount, request.UserId, request.AuctionId);

            return Ok(new { message = "Amount released successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing amount for user {UserId}", request.UserId);
            return StatusCode(500, "An error occurred while releasing the amount");
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

    private async Task<List<BlockedAmountDetail>> GetBlockedAmountDetails(string userId)
    {
        // Get active bids where user is the highest bidder
        var activeBids = await _context.Auctions
            .Include(a => a.Asset)
            .Where(a => a.CurrentHighestBidderId == userId && 
                       a.Status == AuctionStatus.Live)
            .ToListAsync();

        return activeBids.Select(auction => new BlockedAmountDetail
        {
            AuctionId = auction.Id,
            AssetTitle = auction.Asset?.Title ?? "Unknown Asset",
            BidAmount = auction.CurrentHighestBid,
            BidDate = auction.StartDate, // This should be updated when bid history is implemented
            AuctionStatus = auction.Status.ToString()
        }).ToList();
    }

    private static Models.WalletTransaction MapToWalletTransactionDto(Domain.Entities.WalletTransaction transaction)
    {
        return new Models.WalletTransaction
        {
            Id = transaction.Id,
            Type = transaction.Type.ToString(),
            Amount = transaction.Amount,
            TransactionDate = transaction.TransactionDate,
            Description = transaction.Description,
            Status = transaction.Status.ToString()
        };
    }

    #endregion
}

// Additional DTOs for internal wallet operations
public class BlockAmountRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
    
    public int? AuctionId { get; set; }
    
    [Required]
    public string Description { get; set; } = string.Empty;
}

public class ReleaseAmountRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
    
    public int? AuctionId { get; set; }
    
    [Required]
    public string Description { get; set; } = string.Empty;
} 