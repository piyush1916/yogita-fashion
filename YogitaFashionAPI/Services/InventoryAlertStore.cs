namespace YogitaFashionAPI.Services
{
    public static class InventoryAlertStore
    {
        private static readonly object Sync = new();
        private static readonly HashSet<int> LowStockAlertSentProducts = new();

        public static bool IsLowStockAlertSent(int productId)
        {
            lock (Sync)
            {
                return LowStockAlertSentProducts.Contains(productId);
            }
        }

        public static void MarkLowStockAlertSent(int productId)
        {
            lock (Sync)
            {
                LowStockAlertSentProducts.Add(productId);
            }
        }

        public static void ClearLowStockAlert(int productId)
        {
            lock (Sync)
            {
                LowStockAlertSentProducts.Remove(productId);
            }
        }
    }
}
