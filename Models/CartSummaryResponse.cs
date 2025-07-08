public record CartSummaryResponse(
    string UserId,
    int ItemCount,
    decimal SubTotal,
    decimal TaxAmount,
    decimal ShippingAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string Currency,
    List<CartItemResponse> Items,
    List<string> AppliedCoupons,
    CartStatus Status
);