public class InMemoryProductRepository : IProductRepository
{
    private readonly List<Product> _products;

    public InMemoryProductRepository()
    {
        _products = GenerateMockProducts();
    }

    public Task<Product?> GetProductAsync(int productId)
    {
        var product = _products.FirstOrDefault(p => p.Id == productId);
        return Task.FromResult(product);
    }

    public Task<List<Product>> GetProductsAsync()
    {
        return Task.FromResult(_products.Where(p => p.IsActive).ToList());
    }

    public Task<List<Product>> SearchProductsAsync(string query)
    {
        var results = _products
            .Where(p => p.IsActive &&
                        (p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                         p.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return Task.FromResult(results);
    }

    public Task UpdateStockAsync(int productId, int quantity)
    {
        var product = _products.FirstOrDefault(p => p.Id == productId);
        if (product != null)
        {
            product.StockQuantity = Math.Max(0, product.StockQuantity - quantity);
            product.UpdatedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    private List<Product> GenerateMockProducts()
    {
        var random = new Random();
        var categories = new[] {"Electronics", "Clothing", "Books", "Home", "Sports", "Beauty"};
        var products = new List<Product>();

        for (var i = 1; i <= 100; i++)
            products.Add(new Product
            {
                Id = i,
                Name = $"Product {i}",
                Description = $"Description for product {i}",
                Price = random.Next(10, 1000),
                StockQuantity = random.Next(0, 100),
                Category = categories[random.Next(categories.Length)],
                ImageUrl = $"https://picsum.photos/200/200?random={i}",
                IsActive = random.NextDouble() > 0.1,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(365)),
                UpdatedAt = DateTime.UtcNow.AddDays(-random.Next(30)),
                Tags = [$"tag{i}", $"category{i % 5}"],
                Status = (ProductStatus) random.Next(0, 5)
            });

        return products;
    }
}