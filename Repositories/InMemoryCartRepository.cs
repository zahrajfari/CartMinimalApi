public class InMemoryCartRepository : ICartRepository
{
    private readonly Dictionary<string, ShoppingCart> _carts = new();
    private readonly Dictionary<string, ShoppingCart> _sharedCarts = new();

    public Task<ShoppingCart?> GetCartAsync(string userId)
    {
        _carts.TryGetValue(userId, out var cart);
        return Task.FromResult(cart);
    }

    public Task SaveCartAsync(ShoppingCart cart)
    {
        _carts[cart.UserId] = cart;

        if (!string.IsNullOrEmpty(cart.SharedToken)) _sharedCarts[cart.SharedToken] = cart;

        return Task.CompletedTask;
    }

    public Task DeleteCartAsync(string userId)
    {
        _carts.Remove(userId);
        return Task.CompletedTask;
    }

    public Task<ShoppingCart?> GetSharedCartAsync(string shareToken)
    {
        _sharedCarts.TryGetValue(shareToken, out var cart);
        return Task.FromResult(cart);
    }

    public Task<List<ShoppingCart>> GetExpiredCartsAsync()
    {
        var expiredCarts = _carts.Values
            .Where(c => c.ExpiresAt.HasValue && c.ExpiresAt < DateTime.UtcNow)
            .ToList();
        return Task.FromResult(expiredCarts);
    }
}