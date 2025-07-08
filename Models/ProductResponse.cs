public record ProductResponse(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    int StockQuantity,
    string Category,
    string ImageUrl,
    bool IsActive,
    List<string> Tags,
    ProductStatus Status
);