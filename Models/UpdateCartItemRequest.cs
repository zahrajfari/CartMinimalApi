public record UpdateCartItemRequest(
    int Quantity,
    string? Notes = null
);