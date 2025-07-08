public interface IDiscountService
{
    Task<Discount?> GetDiscountAsync(string code);
    Task<decimal> CalculateDiscountAsync(ShoppingCart cart, string couponCode);
    Task ApplyDiscountAsync(ShoppingCart cart, string couponCode);
}