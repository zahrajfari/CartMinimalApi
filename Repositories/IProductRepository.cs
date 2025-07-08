public interface IProductRepository
{
    Task<Product?> GetProductAsync(int productId);
    Task<List<Product>> GetProductsAsync();
    Task<List<Product>> SearchProductsAsync(string query);
    Task UpdateStockAsync(int productId, int quantity);
}