using Confluent.Kafka;
using System.Text.Json;
using MatchMaking.Shared.Constants;
using MatchMaking.Shared.Contracts;
using StackExchange.Redis;

namespace MatchMaking.Worker;

public class MatchMakingWorker(
    IConsumer<string, string> consumer,
    IProducer<string, string> producer,
    ILogger<MatchMakingWorker> logger,
    IConfiguration configuration,
    IConnectionMultiplexer redis)
    : BackgroundService
{
    private readonly string _requestTopic = KafkaTopics.MatchRequest;
    private readonly string _completeTopic = KafkaTopics.MatchComplete;
    private readonly string _queueKey = configuration.GetValue<string>("MatchMaking:QueueKey", "matchmaking:queue");
    private readonly int _playersPerMatch = configuration.GetValue("MatchMaking:PlayersPerMatch", 3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe(_requestTopic);
        logger.LogInformation("Worker started. Consuming from {Topic}. Players per match: {Count}",
            _requestTopic, _playersPerMatch);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromMilliseconds(100));

                    if (result?.Message?.Value != null)
                    {
                        var userId = result.Message.Value;
                        logger.LogInformation("Received match request from user: {UserId}", userId);

                        
                        var db = redis.GetDatabase();
                        await db.ListRightPushAsync(_queueKey, userId);
                        
                        consumer.Commit(result);

                        
                        var queueSize = await db.ListLengthAsync(_queueKey);
                        logger.LogInformation("Queue size: {Size}/{Required}", queueSize, _playersPerMatch);

                        if (queueSize >= _playersPerMatch)
                        {
                            await TryCreateMatch(db, stoppingToken);
                        }
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    logger.LogWarning($"Topic {_requestTopic} not yet available, retrying...");
                    await Task.Delay(2000, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    logger.LogError(ex, "Error consuming message from Kafka");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            consumer.Close();
            producer.Dispose();
        }
    }
    
    private async Task TryCreateMatch(IDatabase db, CancellationToken stoppingToken)
    {
        var transaction = db.CreateTransaction();
        
        var playersTask = transaction.ListRangeAsync(_queueKey, 0, _playersPerMatch - 1);
        _ = transaction.ListTrimAsync(_queueKey, _playersPerMatch, -1);
        
        if (!await transaction.ExecuteAsync())
        {
            logger.LogWarning("Failed to acquire players from queue (race condition)");
            return;
        }

        var players = await playersTask;
        
        if (players.Length < _playersPerMatch)
        {
            logger.LogWarning("Not enough players in queue after transaction");
            return;
        }

        await CreateMatch(players.Select(p => p.ToString()).ToArray()!, stoppingToken);
    }

    private async Task CreateMatch(string[] playersForMatch, CancellationToken stoppingToken)
    {
        var matchId = Guid.NewGuid().ToString();
        var matchComplete = new MatchCompleteMessage(matchId, playersForMatch);
        var messageJson = JsonSerializer.Serialize(matchComplete);

        try
        {
            var message = new Message<string, string>
            {
                Key = matchId,
                Value = messageJson
            };

            await producer.ProduceAsync(_completeTopic, message, stoppingToken);

            logger.LogInformation("Match {MatchId} created with players: {Players}",
                matchId, string.Join(", ", playersForMatch));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send match complete message for match {MatchId}", matchId);

            var db = redis.GetDatabase();
            foreach (var player in playersForMatch.Reverse())
            {
                await db.ListLeftPushAsync(_queueKey, player);
            }
        }
    }
}
