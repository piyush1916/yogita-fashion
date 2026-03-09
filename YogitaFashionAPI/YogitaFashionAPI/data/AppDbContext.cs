using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using YogitaFashionAPI.Models;

namespace YogitaFashionAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponUsageRecord> CouponUsageRecords { get; set; }
        public DbSet<ReturnRequest> ReturnRequests { get; set; }
        public DbSet<SupportRequest> SupportRequests { get; set; }
        public DbSet<StockAlertSubscription> StockAlertSubscriptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var stringListConverter = new ValueConverter<List<string>, string>(
                value => Serialize(value),
                value => Deserialize<List<string>>(value) ?? new List<string>());

            var orderItemsConverter = new ValueConverter<List<OrderItem>, string>(
                value => Serialize(value),
                value => Deserialize<List<OrderItem>>(value) ?? new List<OrderItem>());

            var statusHistoryConverter = new ValueConverter<List<OrderStatusHistoryEntry>, string>(
                value => Serialize(value),
                value => Deserialize<List<OrderStatusHistoryEntry>>(value) ?? new List<OrderStatusHistoryEntry>());

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("products");
                entity.Property(item => item.Sizes)
                    .HasConversion(stringListConverter)
                    .HasColumnName("SizesJson")
                    .HasColumnType("longtext");
                entity.Property(item => item.Colors)
                    .HasConversion(stringListConverter)
                    .HasColumnName("ColorsJson")
                    .HasColumnType("longtext");
            });

            modelBuilder.Entity<User>().ToTable("users");

            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("orders");
                entity.Property(item => item.Total).HasColumnName("TotalAmount");
                entity.Property(item => item.CreatedAt).HasColumnName("OrderDate");
                entity.Property(item => item.Items)
                    .HasConversion(orderItemsConverter)
                    .HasColumnName("ItemsJson")
                    .HasColumnType("longtext");
                entity.Property(item => item.StatusHistory)
                    .HasConversion(statusHistoryConverter)
                    .HasColumnName("StatusHistoryJson")
                    .HasColumnType("longtext");
            });

            modelBuilder.Entity<Address>(entity =>
            {
                entity.ToTable("addresses");
                entity.Property(item => item.Pincode).HasColumnName("ZipCode");
            });

            modelBuilder.Entity<WishlistItem>().ToTable("wishlistitems");

            modelBuilder.Entity<Coupon>(entity =>
            {
                entity.ToTable("coupons");
                entity.Property(item => item.EndAt).HasColumnName("ExpiryDate");
            });

            modelBuilder.Entity<CouponUsageRecord>(entity =>
            {
                entity.ToTable("couponusagerecords");
                entity.HasKey(item => new { item.CouponId, item.UserId });
            });

            modelBuilder.Entity<ReturnRequest>(entity =>
            {
                entity.ToTable("returnrequests");
                entity.Property(item => item.ItemProductId).HasColumnName("ItemProductCode");
            });

            modelBuilder.Entity<SupportRequest>(entity =>
            {
                entity.ToTable("supportrequests");
                entity.Property(item => item.OrderId).HasColumnName("OrderReference");
            });

            modelBuilder.Entity<StockAlertSubscription>().ToTable("stockalertsubscriptions");
        }

        private static string Serialize<TValue>(TValue value)
        {
            return JsonSerializer.Serialize(value);
        }

        private static TValue? Deserialize<TValue>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<TValue>(json);
            }
            catch
            {
                return default;
            }
        }
    }
}
