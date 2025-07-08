public record CartItemResponse(
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string Currency,
    CartItemStatus Status,
    bool InStock,
    string? Notes
);