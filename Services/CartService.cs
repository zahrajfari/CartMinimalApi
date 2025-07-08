public class CartService : ICartService
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ICartRepository _cartRepository;
    private readonly IEventBus _eventBus;
    private readonly IProductRepository _productRepository;

    public CartService(
        ICartRepository cartRepository,
        IProductRepository productRepository,
        IEventBus eventBus,
        IAnalyticsService analyticsService)
    {
        _cartRepository = cartRepository;
        _productRepository = productRepository;
        _eventBus = eventBus;
        _analyticsService = analyticsService;
    }

    public async Task<ShoppingCart> GetOrCreateCartAsync(string userId)
    {
        var cart = await _cartRepository.GetCartAsync(userId);
        if (cart == null)
        {
            cart = new ShoppingCart
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
            await _cartRepository.SaveCartAsync(cart);
        }

        return cart;
    }

    public async Task<CartItem> AddToCartAsync(string userId, AddToCartRequest request)
    {
        var cart = await GetOrCreateCartAsync(userId);
        var product = await _productRepository.GetProductAsync(request.ProductId);

        if (product == null)
            throw new ArgumentException("Product not found");

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == request.ProductId);

        if (existingItem != null)
        {
            existingItem.Quantity += request.Quantity;
            existingItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newItem = new CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = request.Quantity,
                UnitPrice = product.Price,
                Currency = product.Currency,
                AddedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Notes = request.Notes ?? string.Empty
            };
            cart.Items.Add(newItem);
            existingItem = newItem;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _cartRepository.SaveCartAsync(cart);

        await _analyticsService.TrackEventAsync(new AnalyticsEvent
        {
            EventType = "item_added_to_cart",
            UserId = userId,
            Properties = new Dictionary<string, object>
            {
                ["product_id"] = request.ProductId,
                ["quantity"] = request.Quantity,
                ["product_name"] = product.Name
            }
        });

        return existingItem;
    }

    public async Task<CartItem> UpdateCartItemAsync(string userId, int productId, UpdateCartItemRequest request)
    {
        var cart = await GetOrCreateCartAsync(userId);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (item == null)
            throw new ArgumentException("Item not found in cart");

        item.Quantity = request.Quantity;
        item.UpdatedAt = DateTime.UtcNow;
        item.Notes = request.Notes ?? item.Notes;

        cart.UpdatedAt = DateTime.UtcNow;
        await _cartRepository.SaveCartAsync(cart);

        return item;
    }

    public async Task RemoveFromCartAsync(string userId, int productId)
    {
        var cart = await GetOrCreateCartAsync(userId);
        var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

        if (item != null)
        {
            cart.Items.Remove(item);
            cart.UpdatedAt = DateTime.UtcNow;
            await _cartRepository.SaveCartAsync(cart);
        }
    }

    public async Task ClearCartAsync(string userId)
    {
        var cart = await GetOrCreateCartAsync(userId);
        cart.Items.Clear();
        cart.UpdatedAt = DateTime.UtcNow;
        await _cartRepository.SaveCartAsync(cart);
    }

    public async Task<InventoryCheckResponse> CheckInventoryAsync(string userId)
    {
        var cart = await GetOrCreateCartAsync(userId);
        var inventoryItems = new List<InventoryItemStatus>();
        var issues = new List<string>();
        var allInStock = true;

        foreach (var item in cart.Items.Where(i => i.Status == CartItemStatus.Active))
        {
            var product = await _productRepository.GetProductAsync(item.ProductId);
            if (product == null)
            {
                issues.Add($"Product {item.ProductName} is no longer available");
                allInStock = false;
                continue;
            }

            var inStock = product.StockQuantity >= item.Quantity && product.Status == ProductStatus.Available;
            if (!inStock)
            {
                allInStock = false;
                issues.Add(
                    $"Insufficient stock for {product.Name}. Available: {product.StockQuantity}, Requested: {item.Quantity}");
            }

            inventoryItems.Add(new InventoryItemStatus(
                item.ProductId,
                product.Name,
                item.Quantity,
                product.StockQuantity,
                inStock,
                product.Status
            ));
        }

        return new InventoryCheckResponse(allInStock, inventoryItems, issues);
    }

    public async Task<ShareCartResponse> ShareCartAsync(string userId)
    {
        var cart = await GetOrCreateCartAsync(userId);
        var shareToken = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        cart.SharedToken = shareToken;
        cart.Status = CartStatus.Shared;
        await _cartRepository.SaveCartAsync(cart);

        return new ShareCartResponse(
            shareToken,
            $"/api/v1/carts/shared/{shareToken}",
            expiresAt
        );
    }
}