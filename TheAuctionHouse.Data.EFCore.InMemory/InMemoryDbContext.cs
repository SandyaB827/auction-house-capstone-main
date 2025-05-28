namespace TheAuctionHouse.Data.EFCore.InMemory;

using Microsoft.EntityFrameworkCore;

using TheAuctionHouse.Domain.Entities;
using TheAuctionHouse.Domain.DataContracts;
using System.Linq;

public class InMemoryAppDbContext : DbContext, IAppDbContext
    {
        public InMemoryAppDbContext(DbContextOptions<InMemoryAppDbContext> options)
            : base(options)
        {
            PortalUsers = Set<PortalUser>();
            Assets = Set<Asset>();
            Auctions = Set<Auction>();
            BidHistories = Set<BidHistory>();
        }

        public DbSet<T>? GetDbSet<T>() where T : class
        {
            if (typeof(T) == typeof(PortalUser))
                return PortalUsers as DbSet<T>;
            if (typeof(T) == typeof(Asset))
                return Assets as DbSet<T>;
            if (typeof(T) == typeof(Auction))
                return Auctions as DbSet<T>;
            if (typeof(T) == typeof(BidHistory))
                return BidHistories as DbSet<T>;

            throw new ArgumentException("Invalid type");
        }

        public DbSet<PortalUser> PortalUsers { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<Auction> Auctions { get; set; }
        public DbSet<BidHistory> BidHistories { get; set; }

    IQueryable<PortalUser> IAppDbContext.PortalUsers => PortalUsers;

    IQueryable<Asset> IAppDbContext.Assets => Assets;

    IQueryable<Auction> IAppDbContext.Auctions => Auctions;

    IQueryable<BidHistory> IAppDbContext.BidHistories => BidHistories;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseInMemoryDatabase("InMemoryDb");
            }
        }

        // Define your DbSets here
        // public DbSet<YourEntity> YourEntities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure your entity relationships and constraints here
        }

    }