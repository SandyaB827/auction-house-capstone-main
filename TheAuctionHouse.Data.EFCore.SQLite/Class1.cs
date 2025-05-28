using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TheAuctionHouse.Domain.Entities;

namespace TheAuctionHouse.Data.EFCore.SQLite;

public class AuctionHouseDbContext : IdentityDbContext<PortalUser>
{
    public DbSet<Asset> Assets { get; set; }
    public DbSet<Auction> Auctions { get; set; }
    public DbSet<BidHistory> BidHistories { get; set; }
    public DbSet<WalletTransaction> WalletTransactions { get; set; }

    public AuctionHouseDbContext(DbContextOptions<AuctionHouseDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // Essential for Identity tables

        // Asset Configuration
        builder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.RetailValue).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();

            entity.HasOne(e => e.Owner)
                  .WithMany(u => u.AssetsOwned)
                  .HasForeignKey(e => e.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Auction Configuration
        builder.Entity<Auction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReservedPrice).IsRequired();
            entity.Property(e => e.MinimumBidIncrement).IsRequired();
            entity.Property(e => e.CurrentHighestBid).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasConversion<string>();

            entity.HasOne(e => e.Asset)
                  .WithMany(a => a.Auctions)
                  .HasForeignKey(e => e.AssetId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Seller)
                  .WithMany(u => u.AuctionsListedAsSeller)
                  .HasForeignKey(e => e.SellerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CurrentHighestBidder)
                  .WithMany()
                  .HasForeignKey(e => e.CurrentHighestBidderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // BidHistory Configuration
        builder.Entity<BidHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BidAmount).HasPrecision(18, 2);
            entity.Property(e => e.BidderName).IsRequired().HasMaxLength(100);

            entity.HasOne(e => e.Auction)
                  .WithMany(a => a.BidHistories)
                  .HasForeignKey(e => e.AuctionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Bidder)
                  .WithMany(u => u.BidsPlaced)
                  .HasForeignKey(e => e.BidderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // PortalUser Configuration
        builder.Entity<PortalUser>(entity =>
        {
            entity.Property(e => e.WalletBalance).HasPrecision(18, 2);
            entity.Property(e => e.BlockedAmount).HasPrecision(18, 2);
            entity.Property(e => e.FirstName).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(50);
        });

        // WalletTransaction Configuration
        builder.Entity<WalletTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();

            entity.HasOne(e => e.User)
                  .WithMany(u => u.WalletTransactions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.RelatedAuction)
                  .WithMany()
                  .HasForeignKey(e => e.RelatedAuctionId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.RelatedAsset)
                  .WithMany()
                  .HasForeignKey(e => e.RelatedAssetId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
