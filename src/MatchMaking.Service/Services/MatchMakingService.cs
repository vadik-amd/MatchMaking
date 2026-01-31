using Confluent.Kafka;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using MatchMaking.Shared.Constants;
using MatchMaking.Shared.Contracts;

namespace MatchMaking.Service.Services;

public class MatchMakingService(
    IDistributedCache cache,
    IProducer<string, string> producer,
    ILogger<MatchMakingService> logger)
    : IMatchMakingService
{
    private readonly string _requestTopic = KafkaTopics.MatchRequest;

    public async Task RequestMatchAsync(string userId)
    {
        var rateLimitKey = $"ratelimit:{userId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var lastRequestData = await cache.GetStringAsync(rateLimitKey);
        
        if (!string.IsNullOrEmpty(lastRequestData))
        {
            if (long.TryParse(lastRequestData, out var lastRequestMs))
            {
                var elapsed = now - lastRequestMs;

                if (elapsed < 100)
                {
                    logger.LogWarning("Rate limit exceeded for user {UserId}, elapsed: {Elapsed}ms", 
                        userId, elapsed);
                    throw new InvalidOperationException(
                        $"Rate limit exceeded. Please wait {100 - elapsed}ms");
                }
            }
        }

        await cache.SetStringAsync(
            rateLimitKey, 
            now.ToString(),
            new DistributedCacheEntryOptions 
            { 
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)
            });

        try
        {
            var message = new Message<string, string>
            {
                Key = userId,
                Value = userId
            };

            await producer.ProduceAsync(_requestTopic, message);
            logger.LogInformation("Match request sent to Kafka for user {UserId}", userId);
        }
        catch (ProduceException<string, string> ex)
        {
            logger.LogError(ex, "Failed to send match request to Kafka for user {UserId}", userId);
            throw new InvalidOperationException("Failed to process match request", ex);
        }
    }

    public async Task<MatchInfoResponse?> GetMatchInfoAsync(string userId)
    {
        try
        {
            var cacheKey = $"match:{userId}";
            var cachedData = await cache.GetStringAsync(cacheKey);

            if (string.IsNullOrEmpty(cachedData))
            {
                logger.LogInformation("No match found in cache for user {UserId}", userId);
                return null;
            }

            var matchInfo = JsonSerializer.Deserialize<MatchInfoResponse>(cachedData);
            logger.LogInformation("Match info retrieved for user {UserId}: {MatchId}", 
                userId, matchInfo?.MatchId);
            
            return matchInfo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving match info from cache for user {UserId}", userId);
            throw;
        }
    }
    
    public async Task SaveMatchInfoAsync(string matchId, string[] userIds)
    {
        try
        {
            var matchInfo = new MatchInfoResponse(matchId, userIds);
            var jsonData = JsonSerializer.Serialize(matchInfo);

            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            };

            foreach (var userId in userIds)
            {
                var cacheKey = $"match:{userId}";
                await cache.SetStringAsync(cacheKey, jsonData, cacheOptions);
                logger.LogInformation("Match info saved for user {UserId}, match {MatchId}", 
                    userId, matchId);
            }

            logger.LogInformation("Match making completed for match {MatchId} with {UserCount} users", 
                matchId, userIds.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save match info for match {MatchId}", matchId);
            throw;
        }
    }
}