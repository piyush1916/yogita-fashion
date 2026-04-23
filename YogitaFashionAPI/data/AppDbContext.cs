using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        public DbSet<OrderLineItem> OrderLineItems { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<Coupon> Coupons { get; set; }
        public DbSet<CouponUsageRecord> CouponUsageRecords { get; set; }
        public DbSet<ReturnRequest> ReturnRequests { get; set; }
        public DbSet<SupportRequest> SupportRequests { get; set; }
        public DbSet<StockAlertSubscription> StockAlertSubscriptions { get; set; }
        public DbSet<AuditLogEntry> AuditLogEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var stringListConverter = new ValueConverter<List<string>, string>(
                value => Serialize(value),
                value => Deserialize<List<string>>(value) ?? new List<string>());
            var stringListComparer = CreateJsonValueComparer<List<string>>();

            var orderItemsConverter = new ValueConverter<List<OrderItem>, string>(
                value => Serialize(value),
                value => Deserialize<List<OrderItem>>(value) ?? new List<OrderItem>());
            var orderItemsComparer = CreateJsonValueComparer<List<OrderItem>>();

            var statusHistoryConverter = new ValueConverter<List<OrderStatusHistoryEntry>, string>(
                value => Serialize(value),
                value => Deserialize<List<OrderStatusHistoryEntry>>(value) ?? new List<OrderStatusHistoryEntry>());
            var statusHistoryComparer = CreateJsonValueComparer<List<OrderStatusHistoryEntry>>();

            var productDetailsConverter = new ValueConverter<ProductDetails, string>(
                value => Serialize(value),
                value => Deserialize<ProductDetails>(value) ?? new ProductDetails());
            var productDetailsComparer = CreateJsonValueComparer<ProductDetails>();

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.Name).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Email).HasMaxLength(191).IsRequired();
                entity.Property(item => item.Password).HasMaxLength(255).IsRequired();
                entity.Property(item => item.Phone).HasMaxLength(30).IsRequired().HasDefaultValue("");
                entity.Property(item => item.City).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Role).HasMaxLength(50).IsRequired().HasDefaultValue("Customer");
                entity.Property(item => item.CreatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.HasIndex(item => item.Email).IsUnique();
                entity.HasIndex(item => item.Role);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("products");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.Name).HasMaxLength(150).IsRequired();
                entity.Property(item => item.Price).HasColumnType("decimal(10,2)").IsRequired();
                entity.Property(item => item.OriginalPrice).HasColumnType("decimal(10,2)").IsRequired();
                entity.Property(item => item.Mrp).HasColumnType("decimal(10,2)").IsRequired();
                entity.Property(item => item.Category).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Brand).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.ImageUrl).HasMaxLength(500).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Description).HasColumnType("longtext").IsRequired();
                entity.Property(item => item.Size).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Color).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Stock).IsRequired().HasDefaultValue(0);
                entity.Property(item => item.FeaturedProduct).IsRequired().HasDefaultValue(false);
                entity.Property(item => item.IsBestSeller).IsRequired().HasDefaultValue(false);
                entity.Property(item => item.CreatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.Property(item => item.UpdatedAt).HasColumnType("datetime(6)").IsRequired();

                var sizesProperty = entity.Property(item => item.Sizes)
                    .HasConversion(stringListConverter)
                    .HasColumnName("SizesJson")
                    .HasColumnType("longtext")
                    .IsRequired();
                sizesProperty.Metadata.SetValueComparer(stringListComparer);

                var colorsProperty = entity.Property(item => item.Colors)
                    .HasConversion(stringListConverter)
                    .HasColumnName("ColorsJson")
                    .HasColumnType("longtext")
                    .IsRequired();
                colorsProperty.Metadata.SetValueComparer(stringListComparer);

                var detailsProperty = entity.Property(item => item.Details)
                    .HasConversion(productDetailsConverter)
                    .HasColumnName("DetailsJson")
                    .HasColumnType("longtext")
                    .IsRequired();
                detailsProperty.Metadata.SetValueComparer(productDetailsComparer);

                entity.HasIndex(item => item.Category);
                entity.HasIndex(item => item.Stock);
                entity.HasIndex(item => item.FeaturedProduct);
                entity.HasIndex(item => item.IsBestSeller);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("orders");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.Total).HasColumnName("TotalAmount").HasColumnType("decimal(10,2)").IsRequired();
                entity.Property(item => item.CreatedAt).HasColumnName("OrderDate").HasColumnType("datetime(6)").IsRequired();
                entity.Property(item => item.UpdatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.Property(item => item.Name).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Phone).HasMaxLength(30).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Email).HasMaxLength(191).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Address).HasMaxLength(500).IsRequired().HasDefaultValue("");
                entity.Property(item => item.City).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Pincode).HasMaxLength(20).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Payment).HasMaxLength(50).IsRequired().HasDefaultValue("COD");
                entity.Property(item => item.Status).HasMaxLength(50).IsRequired().HasDefaultValue("Pending");
                entity.Property(item => item.OrderNumber).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.TrackingNumber).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.CouponCode).HasMaxLength(80).IsRequired().HasDefaultValue("");
                entity.Property(item => item.DiscountAmount).HasColumnType("decimal(10,2)").IsRequired();

                var itemsProperty = entity.Property(item => item.Items)
                    .HasConversion(orderItemsConverter)
                    .HasColumnName("ItemsJson")
                    .HasColumnType("longtext")
                    .IsRequired();
                itemsProperty.Metadata.SetValueComparer(orderItemsComparer);

                var statusHistoryProperty = entity.Property(item => item.StatusHistory)
                    .HasConversion(statusHistoryConverter)
                    .HasColumnName("StatusHistoryJson")
                    .HasColumnType("longtext")
                    .IsRequired();
                statusHistoryProperty.Metadata.SetValueComparer(statusHistoryComparer);

                entity.HasOne(item => item.User)
                    .WithMany(item => item.Orders)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(item => item.Coupon)
                    .WithMany(item => item.Orders)
                    .HasForeignKey(item => item.CouponId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(item => item.UserId);
                entity.HasIndex(item => item.CouponId);
                entity.HasIndex(item => item.Status);
                entity.HasIndex(item => item.CreatedAt);
                entity.HasIndex(item => item.TrackingNumber);
                entity.HasIndex(item => item.OrderNumber).IsUnique();
            });

            modelBuilder.Entity<OrderLineItem>(entity =>
            {
                entity.ToTable("orderitems");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.ItemKey).HasMaxLength(120).IsRequired();
                entity.Property(item => item.Title).HasMaxLength(180).IsRequired();
                entity.Property(item => item.Image).HasMaxLength(500).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Price).HasColumnType("decimal(10,2)").IsRequired();
                entity.Property(item => item.Mrp).HasColumnType("decimal(10,2)").IsRequired();
                entity.Property(item => item.Category).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Size).HasMaxLength(80).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Color).HasMaxLength(80).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Quantity).IsRequired().HasDefaultValue(1);
                entity.Property(item => item.CreatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.HasOne(item => item.Order)
                    .WithMany(item => item.LineItems)
                    .HasForeignKey(item => item.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Product)
                    .WithMany(item => item.OrderLineItems)
                    .HasForeignKey(item => item.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(item => item.ProductId);
                entity.HasIndex(item => new { item.OrderId, item.ItemKey }).IsUnique();
            });

            modelBuilder.Entity<Address>(entity =>
            {
                entity.ToTable("addresses");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.FullName).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Phone).HasMaxLength(30).IsRequired().HasDefaultValue("");
                entity.Property(item => item.City).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.State).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Pincode).HasColumnName("ZipCode").HasMaxLength(20).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Country).HasMaxLength(100).IsRequired().HasDefaultValue("India");
                entity.Property(item => item.Street).HasMaxLength(300).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Line2).HasMaxLength(255).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Landmark).HasMaxLength(255).IsRequired().HasDefaultValue("");
                entity.Property(item => item.AddressType).HasMaxLength(30).IsRequired().HasDefaultValue("Home");
                entity.Property(item => item.IsDefault).IsRequired().HasDefaultValue(false);
                entity.Property(item => item.CreatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.Property(item => item.UpdatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.HasOne(item => item.User)
                    .WithMany(item => item.Addresses)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(item => item.UserId);
                entity.HasIndex(item => new { item.UserId, item.IsDefault });
            });

            modelBuilder.Entity<WishlistItem>(entity =>
            {
                entity.ToTable("wishlistitems");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.CreatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.HasOne(item => item.User)
                    .WithMany(item => item.WishlistItems)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Product)
                    .WithMany(item => item.WishlistItems)
                    .HasForeignKey(item => item.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(item => new { item.UserId, item.ProductId }).IsUnique();
                entity.HasIndex(item => item.ProductId);
            });

            modelBuilder.Entity<Coupon>(entity =>
            {
                entity.ToTable("coupons");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.Code).HasMaxLength(50).IsRequired();
                entity.Property(item => item.Type).HasMaxLength(20).IsRequired().HasDefaultValue("percent");
                entity.Property(item => item.Value).HasColumnType("decimal(10,2)").IsRequired();
                entity.Property(item => item.MinOrderAmount).HasColumnType("decimal(10,2)").IsRequired();
                entity.Property(item => item.MaxUses).IsRequired().HasDefaultValue(0);
                entity.Property(item => item.MaxUsesPerUser).IsRequired().HasDefaultValue(1);
                entity.Property(item => item.UsedCount).IsRequired().HasDefaultValue(0);
                entity.Property(item => item.IsActive).IsRequired().HasDefaultValue(true);
                entity.Property(item => item.StartAt).HasColumnType("datetime(6)");
                entity.Property(item => item.EndAt).HasColumnName("ExpiryDate").HasColumnType("datetime(6)");
                entity.Property(item => item.CreatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.Property(item => item.UpdatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.HasIndex(item => item.Code).IsUnique();
                entity.HasIndex(item => new { item.IsActive, item.StartAt, item.EndAt });
            });

            modelBuilder.Entity<CouponUsageRecord>(entity =>
            {
                entity.ToTable("couponusagerecords");
                entity.HasKey(item => new { item.CouponId, item.UserId });
                entity.Property(item => item.Count).IsRequired().HasDefaultValue(0);
                entity.HasOne(item => item.Coupon)
                    .WithMany(item => item.UsageRecords)
                    .HasForeignKey(item => item.CouponId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.User)
                    .WithMany(item => item.CouponUsageRecords)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(item => item.UserId);
            });

            modelBuilder.Entity<ReturnRequest>(entity =>
            {
                entity.ToTable("returnrequests");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.ItemProductId).HasColumnName("ItemProductCode").HasMaxLength(120).IsRequired().HasDefaultValue("");
                entity.Property(item => item.ProductId).HasColumnName("ItemProductId");
                entity.Property(item => item.ItemTitle).HasMaxLength(180).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Quantity).IsRequired().HasDefaultValue(1);
                entity.Property(item => item.RefundAmount).HasColumnType("decimal(10,2)").IsRequired();
                entity.Property(item => item.Reason).HasColumnType("longtext").IsRequired();
                entity.Property(item => item.Status).HasMaxLength(50).IsRequired().HasDefaultValue("Pending");
                entity.Property(item => item.CustomerRemark).HasColumnType("longtext").IsRequired();
                entity.Property(item => item.AdminRemark).HasColumnType("longtext").IsRequired();
                entity.Property(item => item.CreatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.Property(item => item.UpdatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.HasOne(item => item.Order)
                    .WithMany(item => item.ReturnRequests)
                    .HasForeignKey(item => item.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.User)
                    .WithMany(item => item.ReturnRequests)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Product)
                    .WithMany(item => item.ReturnRequests)
                    .HasForeignKey(item => item.ProductId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(item => item.OrderId);
                entity.HasIndex(item => item.UserId);
                entity.HasIndex(item => item.ProductId);
                entity.HasIndex(item => new { item.OrderId, item.ItemProductId });
                entity.HasIndex(item => item.Status);
            });

            modelBuilder.Entity<SupportRequest>(entity =>
            {
                entity.ToTable("supportrequests");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Ignore(item => item.OrderId);
                entity.Property(item => item.OrderDbId).HasColumnName("OrderId");
                entity.Property(item => item.OrderReference).HasMaxLength(120).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Name).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Contact).HasMaxLength(100).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Subject).HasMaxLength(150).IsRequired().HasDefaultValue("General Support");
                entity.Property(item => item.Message).HasColumnType("longtext").IsRequired();
                entity.Property(item => item.Email).HasMaxLength(191).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Phone).HasMaxLength(30).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Status).HasMaxLength(50).IsRequired().HasDefaultValue("Open");
                entity.Property(item => item.CreatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.Property(item => item.UpdatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.HasOne(item => item.User)
                    .WithMany(item => item.SupportRequests)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(item => item.Order)
                    .WithMany(item => item.SupportRequests)
                    .HasForeignKey(item => item.OrderDbId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(item => item.UserId);
                entity.HasIndex(item => item.OrderDbId);
                entity.HasIndex(item => item.Status);
                entity.HasIndex(item => item.CreatedAt);
            });

            modelBuilder.Entity<StockAlertSubscription>(entity =>
            {
                entity.ToTable("stockalertsubscriptions");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.Email).HasMaxLength(191).IsRequired().HasDefaultValue("");
                entity.Property(item => item.WhatsAppNumber).HasMaxLength(30).IsRequired().HasDefaultValue("");
                entity.Property(item => item.CreatedAt).HasColumnType("datetime(6)").IsRequired();
                entity.Property(item => item.NotifiedAt).HasColumnType("datetime(6)");
                entity.HasOne(item => item.User)
                    .WithMany(item => item.StockAlertSubscriptions)
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(item => item.Product)
                    .WithMany(item => item.StockAlertSubscriptions)
                    .HasForeignKey(item => item.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(item => item.UserId);
                entity.HasIndex(item => item.ProductId);
                entity.HasIndex(item => new { item.ProductId, item.Email, item.WhatsAppNumber, item.NotifiedAt });
            });

            modelBuilder.Entity<AuditLogEntry>(entity =>
            {
                entity.ToTable("auditlogentries");
                entity.HasKey(item => item.Id);
                entity.Property(item => item.Id).ValueGeneratedOnAdd();
                entity.Property(item => item.Role).HasMaxLength(40).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Action).HasMaxLength(120).IsRequired();
                entity.Property(item => item.Module).HasMaxLength(120).IsRequired();
                entity.Property(item => item.EntityId).HasMaxLength(120).IsRequired().HasDefaultValue("");
                entity.Property(item => item.TimestampUtc).HasColumnType("datetime(6)").IsRequired();
                entity.Property(item => item.IpAddress).HasMaxLength(64).IsRequired().HasDefaultValue("");
                entity.Property(item => item.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Success");
                entity.Property(item => item.MetadataJson).HasColumnType("longtext").IsRequired();
                entity.Property(item => item.RequestPath).HasMaxLength(255).IsRequired().HasDefaultValue("");
                entity.Property(item => item.HttpMethod).HasMaxLength(10).IsRequired().HasDefaultValue("");
                entity.Property(item => item.PreviousHash).HasMaxLength(128).IsRequired().HasDefaultValue("");
                entity.Property(item => item.EntryHash).HasMaxLength(128).IsRequired().HasDefaultValue("");
                entity.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(item => item.TimestampUtc);
                entity.HasIndex(item => new { item.Module, item.Action });
                entity.HasIndex(item => item.UserId);
                entity.HasIndex(item => item.Status);
            });
        }

        private static string Serialize<TValue>(TValue value)
        {
            return JsonSerializer.Serialize(value);
        }

        private static ValueComparer<TValue> CreateJsonValueComparer<TValue>() where TValue : class
        {
            return new ValueComparer<TValue>(
                (left, right) => string.Equals(Serialize(left), Serialize(right), StringComparison.Ordinal),
                value => Serialize(value).GetHashCode(StringComparison.Ordinal),
                value => Deserialize<TValue>(Serialize(value)) ?? value);
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
