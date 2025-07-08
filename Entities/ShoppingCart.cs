public class ShoppingCart
{
    public string UserId { get; set; } = string.Empty;
    public List<CartItem> Items { get; set; } = new();
    public decimal SubTotal => Items.Where(i => i.Status == CartItemStatus.Active).Sum(i => i.TotalPrice);
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount => SubTotal + TaxAmount + ShippingAmount - DiscountAmount;
    public string Currency { get; set; } = "USD";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public CartStatus Status { get; set; } = CartStatus.Active;
    public List<string> AppliedCoupons { get; set; } = new();
    public string? SharedToken { get; set; }
}