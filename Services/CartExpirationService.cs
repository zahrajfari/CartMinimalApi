public class CartExpirationService : BackgroundService
{
    private readonly ILogger<CartExpirationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public CartExpirationService(IServiceProvider serviceProvider, ILogger<CartExpirationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var cartRepository = scope.ServiceProvider.GetRequiredService<ICartRepository>();
                var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

                var expiredCarts = await cartRepository.GetExpiredCartsAsync();

                foreach (var cart in expiredCarts)
                {
                    cart.Status = CartStatus.Expired;
                    await cartRepository.SaveCartAsync(cart);

                    await analyticsService.TrackEventAsync(new AnalyticsEvent
                    {
                        EventType = "cart_expired",
                        UserId = cart.UserId,
                        Properties = new Dictionary<string, object>
                        {
                            ["item_count"] = cart.Items.Count,
                            ["total_amount"] = cart.TotalAmount
                        }
                    });
                }

                _logger.LogInformation("Processed {Count} expired carts", expiredCarts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired carts");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}