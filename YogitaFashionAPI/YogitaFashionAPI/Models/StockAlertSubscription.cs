namespace YogitaFashionAPI.Models
{
    public class StockAlertSubscription
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Email { get; set; } = "";
        public string WhatsAppNumber { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? NotifiedAt { get; set; }
    }

    public class CreateStockAlertRequest
    {
        public string Email { get; set; } = "";
        public string WhatsAppNumber { get; set; } = "";
    }
}
