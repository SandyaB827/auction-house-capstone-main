using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Services;

public interface IJwtService
{
    Task<string> GenerateTokenAsync(PortalUser user);
    string? ValidateToken(string token);
} 