using SKUApp.Data.EFCore.InMemory;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.Entities;

public class InMemoryAssetRepository : GenericRepository<Asset>, IAssetRepository
{
    public InMemoryAssetRepository(IAppDbContext context) : base(context)
    {
    }

    public Task<List<Asset>> GetAssetsByUserIdAsync(int userId)
    {
        throw new NotImplementedException();
    }
}