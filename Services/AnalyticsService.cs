public class AnalyticsService : IAnalyticsService
{
    private readonly List<AnalyticsEvent> _events = new();

    public Task TrackEventAsync(AnalyticsEvent analyticsEvent)
    {
        _events.Add(analyticsEvent);
        return Task.CompletedTask;
    }

    public Task<Dictionary<string, object>> GetCartMetricsAsync()
    {
        var metrics = new Dictionary<string, object>
        {
            ["total_events"] = _events.Count,
            ["unique_users"] = _events.Select(e => e.UserId).Distinct().Count(),
            ["items_added"] = _events.Count(e => e.EventType == "item_added_to_cart"),
            ["carts_abandoned"] = _events.Count(e => e.EventType == "cart_abandoned"),
            ["conversions"] = _events.Count(e => e.EventType == "checkout_completed")
        };

        return Task.FromResult(metrics);
    }
}