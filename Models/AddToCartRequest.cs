public record AddToCartRequest(
    int ProductId,
    int Quantity,
    string? Notes = null
);