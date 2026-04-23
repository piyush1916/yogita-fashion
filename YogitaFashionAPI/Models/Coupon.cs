using System.Text.Json.Serialization;

namespace YogitaFashionAPI.Models
{
    public class Coupon
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Type { get; set; } = "percent";
        public decimal Value { get; set; }
        public decimal MinOrderAmount { get; set; }
        public int MaxUses { get; set; }
        public int MaxUsesPerUser { get; set; } = 1;
        public int UsedCount { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<Order> Orders { get; set; } = new List<Order>();

        [JsonIgnore]
        public ICollection<CouponUsageRecord> UsageRecords { get; set; } = new List<CouponUsageRecord>();
    }

    public class CouponValidationRequest
    {
        public string Code { get; set; } = "";
        public int UserId { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class CouponValidationResult
    {
        public bool Valid { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal FinalTotal { get; set; }
        public string Message { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public class CouponUsageRecord
    {
        public int CouponId { get; set; }
        public int UserId { get; set; }
        public int Count { get; set; }

        [JsonIgnore]
        public Coupon? Coupon { get; set; }

        [JsonIgnore]
        public User? User { get; set; }
    }
}
