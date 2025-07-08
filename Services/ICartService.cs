public interface ICartService
{
    Task<ShoppingCart> GetOrCreateCartAsync(string userId);
    Task<CartItem> AddToCartAsync(string userId, AddToCartRequest request);
    Task<CartItem> UpdateCartItemAsync(string userId, int productId, UpdateCartItemRequest request);
    Task RemoveFromCartAsync(string userId, int productId);
    Task ClearCartAsync(string userId);
    Task<InventoryCheckResponse> CheckInventoryAsync(string userId);
    Task<ShareCartResponse> ShareCartAsync(string userId);
}