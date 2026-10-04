using System.Text.Json;
using StackExchange.Redis;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan ttl);
    Task RemoveAsync(params string[] keys);
}

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer mux, ILogger<RedisCacheService> logger)
    {
        _redis = mux.GetDatabase();
        _logger = logger;
    }

    // کش فقط یک بهینه‌سازیه، نه منبع اصلی داده. پس اگه Redis خراب بود، خطا نمی‌دیم و فقط از دیتابیس می‌خونیم
    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var value = await _redis.StringGetAsync(key);
            return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GET failed for {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl)
    {
        try
        {
            await _redis.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SET failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(params string[] keys)
    {
        try
        {
            await _redis.KeyDeleteAsync(keys.Select(k => (RedisKey)k).ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis DELETE failed for {Keys}", string.Join(",", keys));
        }
    }
}