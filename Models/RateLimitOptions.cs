public class RateLimitOptions
{
    public int RequestsPerMinute { get; set; } = 100;
    public int BurstLimit { get; set; } = 50;
}