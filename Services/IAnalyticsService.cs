public interface IAnalyticsService
{
    Task TrackEventAsync(AnalyticsEvent analyticsEvent);
    Task<Dictionary<string, object>> GetCartMetricsAsync();
}