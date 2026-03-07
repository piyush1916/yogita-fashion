namespace YogitaFashionAPI.Models
{
    public class ReturnRequest
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public string ItemProductId { get; set; } = "";
        public string ItemTitle { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public decimal RefundAmount { get; set; }
        public string Reason { get; set; } = "";
        public string Status { get; set; } = "Pending";
        public string CustomerRemark { get; set; } = "";
        public string AdminRemark { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CreateReturnRequest
    {
        public int OrderId { get; set; }
        public string ItemProductId { get; set; } = "";
        public string Reason { get; set; } = "";
        public string CustomerRemark { get; set; } = "";
    }

    public class UpdateReturnStatusRequest
    {
        public string Status { get; set; } = "";
        public string AdminRemark { get; set; } = "";
    }
}
