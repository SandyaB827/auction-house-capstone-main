using TheAuctionHouse.Domain.DataContracts;

public interface IAppUnitOfWork : IDisposable
{
    IAssetRepository AssetRepository { get; }
    IAuctionRepository AuctionRepository { get; }
    IPortalUserRepository PortalUserRepository { get; }

    Task<int> SaveChangesAsync();
}