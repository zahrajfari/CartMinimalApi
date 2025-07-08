public interface ICartRepository
{
    Task<ShoppingCart?> GetCartAsync(string userId);
    Task SaveCartAsync(ShoppingCart cart);
    Task DeleteCartAsync(string userId);
    Task<ShoppingCart?> GetSharedCartAsync(string shareToken);
    Task<List<ShoppingCart>> GetExpiredCartsAsync();
}