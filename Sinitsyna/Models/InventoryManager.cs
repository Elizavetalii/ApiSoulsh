namespace Sinitsyna.Models
{
    public static class InventoryManager
    {
        private static Dictionary<int, int> reservedQuantities = new Dictionary<int, int>();

        public static void ReserveProduct(int productId, int quantity)
        {
            if (reservedQuantities.ContainsKey(productId))
            {
                reservedQuantities[productId] += quantity;
            }
            else
            {
                reservedQuantities[productId] = quantity;
            }
        }

        public static void ReleaseProduct(int productId, int quantity)
        {
            if (reservedQuantities.ContainsKey(productId))
            {
                reservedQuantities[productId] -= quantity;
                if (reservedQuantities[productId] <= 0)
                {
                    reservedQuantities.Remove(productId);
                }
            }
        }

        public static int GetReservedQuantity(int productId)
        {
            return reservedQuantities.ContainsKey(productId) ? reservedQuantities[productId] : 0;
        }
    }
}
