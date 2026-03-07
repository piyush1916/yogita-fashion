namespace YogitaFashionAPI.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal Mrp { get; set; }
        public string Category { get; set; } = "";
        public string Brand { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Description { get; set; } = "";
        public string Size { get; set; } = "";
        public string Color { get; set; } = "";
        public List<string> Sizes { get; set; } = new();
        public List<string> Colors { get; set; } = new();
        public int Stock { get; set; }
        public bool FeaturedProduct { get; set; }
        public bool IsBestSeller { get; set; }
    }
}
