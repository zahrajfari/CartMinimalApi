public record InventoryItemStatus(
    int ProductId,
    string ProductName,
    int RequestedQuantity,
    int AvailableQuantity,
    bool InStock,
    ProductStatus Status
);