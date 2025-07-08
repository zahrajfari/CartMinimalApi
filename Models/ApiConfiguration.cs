public class ApiConfiguration
{
    public string JwtSecret { get; set; } = string.Empty;
    public int CartExpirationDays { get; set; } = 30;
    public bool EnableChaosEngineering { get; set; } = false;
    public RateLimitOptions RateLimit { get; set; } = new();
}