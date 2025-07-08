public class DiscountService : IDiscountService
{
    private readonly List<Discount> _discounts;

    public DiscountService()
    {
        _discounts = new List<Discount>
        {
            new()
            {
                Code = "SAVE10", Name = "10% Off", Type = DiscountType.Percentage, Value = 10, IsActive = true,
                UsageLimit = 1000
            },
            new()
            {
                Code = "FREESHIP", Name = "Free Shipping", Type = DiscountType.FreeShipping, Value = 0, IsActive = true,
                UsageLimit = 500
            },
            new()
            {
                Code = "BOGO", Name = "Buy One Get One", Type = DiscountType.BuyOneGetOne, Value = 50, IsActive = true,
                UsageLimit = 100
            }
        };
    }

    public Task<Discount?> GetDiscountAsync(string code)
    {
        var discount = _discounts.FirstOrDefault(d => d.Code == code && d.IsActive);
        return Task.FromResult(discount);
    }

    public Task<decimal> CalculateDiscountAsync(ShoppingCart cart, string couponCode)
    {
        var discount = _discounts.FirstOrDefault(d => d.Code == couponCode && d.IsActive);
        if (discount == null) return Task.FromResult(0m);

        return discount.Type switch
        {
            DiscountType.Percentage => Task.FromResult(cart.SubTotal * discount.Value / 100),
            DiscountType.FixedAmount => Task.FromResult(Math.Min(discount.Value, cart.SubTotal)),
            DiscountType.FreeShipping => Task.FromResult(cart.ShippingAmount),
            _ => Task.FromResult(0m)
        };
    }

    public async Task ApplyDiscountAsync(ShoppingCart cart, string couponCode)
    {
        var discountAmount = await CalculateDiscountAsync(cart, couponCode);
        cart.DiscountAmount += discountAmount;

        if (!cart.AppliedCoupons.Contains(couponCode)) cart.AppliedCoupons.Add(couponCode);
    }
}