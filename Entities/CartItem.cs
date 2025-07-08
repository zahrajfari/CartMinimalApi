public class CartItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
    public string Currency { get; set; } = "USD";
    public DateTime AddedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public CartItemStatus Status { get; set; } = CartItemStatus.Active;
    public string Notes { get; set; } = string.Empty;
}