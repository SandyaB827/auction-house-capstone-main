using System.ComponentModel.DataAnnotations;
using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Models;

public class CreateAssetRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(150, MinimumLength = 10, ErrorMessage = "Title must be between 10 and 150 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Title should not contain special characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 1000 characters")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Retail value is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Retail value must be a positive integer")]
    public int RetailValue { get; set; }
}

public class UpdateAssetRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(150, MinimumLength = 10, ErrorMessage = "Title must be between 10 and 150 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Title should not contain special characters")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 1000 characters")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Retail value is required")]
    [Range(1, int.MaxValue, ErrorMessage = "Retail value must be a positive integer")]
    public int RetailValue { get; set; }
}

public class ChangeAssetStatusRequest
{
    [Required(ErrorMessage = "Status is required")]
    public AssetStatus Status { get; set; }
}

public class AssetResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RetailValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanChangeStatus { get; set; }
}

public class AssetListResponse
{
    public List<AssetResponse> Assets { get; set; } = new();
    public int TotalCount { get; set; }
} 