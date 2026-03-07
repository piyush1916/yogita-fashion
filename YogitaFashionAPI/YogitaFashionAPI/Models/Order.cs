namespace YogitaFashionAPI.Models
{
    public class OrderItem
    {
        public string Key { get; set; } = "";
        public string ProductId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Image { get; set; } = "";
        public decimal Price { get; set; }
        public decimal Mrp { get; set; }
        public string Category { get; set; } = "";
        public string Size { get; set; } = "";
        public string Color { get; set; } = "";
        public int Qty { get; set; } = 1;
    }

    public class Order
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Name { get; set; } = "";

        public string Phone { get; set; } = "";

        public string Email { get; set; } = "";

        public string Address { get; set; } = "";

        public string City { get; set; } = "";

        public string Pincode { get; set; } = "";

        public string Payment { get; set; } = "COD";

        public decimal Total { get; set; }

        public List<OrderItem> Items { get; set; } = new();

        public string Status { get; set; } = "Pending";

        public string OrderNumber { get; set; } = "";

        public string TrackingNumber { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<OrderStatusHistoryEntry> StatusHistory { get; set; } = new();
    }

    public class TrackOrderRequest
    {
        public string OrderId { get; set; } = "";
        public string Contact { get; set; } = "";
    }

    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    public class OrderStatusHistoryEntry
    {
        public string Status { get; set; } = "";
        public string Notes { get; set; } = "";
        public string UpdatedBy { get; set; } = "";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
