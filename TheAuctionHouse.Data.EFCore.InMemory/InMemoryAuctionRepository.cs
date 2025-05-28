using SKUApp.Data.EFCore.InMemory;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.Entities;

public class InMemoryAuctionRepository : GenericRepository<Auction>, IAuctionRepository
{
    public InMemoryAuctionRepository(IAppDbContext context) : base(context)
    {
    }

    public Task<List<Auction>> GetAuctionsByUserIdAsync(int userId)
    {
        throw new NotImplementedException();
    }

    public Task<List<BidHistory>> GetBidHistoriesByAuctionIdAsync(int userId)
    {
        throw new NotImplementedException();
    }

    public Task<List<BidHistory>> GetBidHistoriesByUserIdAsync(int userId)
    {
        throw new NotImplementedException();
    }
}