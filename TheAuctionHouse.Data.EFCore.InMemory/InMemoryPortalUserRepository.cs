using Microsoft.EntityFrameworkCore;
using SKUApp.Data.EFCore.InMemory;
using TheAuctionHouse.Domain.DataContracts;
using TheAuctionHouse.Domain.Entities;

public class InMemoryPortalUserRespository : GenericRepository<PortalUser>, IPortalUserRepository
{
    public InMemoryPortalUserRespository(IAppDbContext context) : base(context)
    {
    }

    public void DepositWalletBalance(int userId, int amount)
    {
        throw new NotImplementedException();
    }

    public async Task<PortalUser?> GetUserByEmailAsync(string email)
    {
        return await this._context.PortalUsers.FirstOrDefaultAsync(x => x.EmailId == email);
    }

    public Task<PortalUser?> GetUserByUserIdAsync(int userId)
    {
        throw new NotImplementedException();
    }
    public void WithdrawWalletBalance(int userId, int amount)
    {
        throw new NotImplementedException();
    }
}