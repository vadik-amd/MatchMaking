using Confluent.Kafka;
using System.Text.Json;
using MatchMaking.Shared.Constants;
using MatchMaking.Shared.Contracts;

namespace MatchMaking.Worker;

public class MatchMakingWorker : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<MatchMakingWorker> _logger;

    private readonly List<string> _waitingPlayers = new();
    private readonly string _requestTopic = KafkaTopics.MatchRequest;
    private readonly string _completeTopic = KafkaTopics.MatchComplete;
    private readonly int _playersPerMatch;

    public MatchMakingWorker(
        IConsumer<string, string> consumer,
        IProducer<string, string> producer,
        ILogger<MatchMakingWorker> logger,
        IConfiguration configuration)
    {
        _consumer = consumer;
        _producer = producer;
        _logger = logger;
        _playersPerMatch = configuration.GetValue<int>("MatchMaking:PlayersPerMatch", 3);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_requestTopic);
        _logger.LogInformation("Worker started. Consuming from {Topic}. Players per match: {Count}",
            _requestTopic, _playersPerMatch);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromMilliseconds(100));

                    if (result?.Message?.Value != null)
                    {
                        var userId = result.Message.Value;
                        _logger.LogInformation("Received match request from user: {UserId}", userId);

                        _waitingPlayers.Add(userId);
                        _consumer.Commit(result);

                        if (_waitingPlayers.Count >= _playersPerMatch)
                        {
                            await CreateMatch(stoppingToken);
                        }
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    _logger.LogWarning($"Topic {_requestTopic} not yet available, retrying...");
                    await Task.Delay(2000, stoppingToken);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message from Kafka");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            _consumer.Close();
            _producer.Dispose();
        }
    }

    private async Task CreateMatch(CancellationToken stoppingToken)
    {
        var playersForMatch = _waitingPlayers.Take(_playersPerMatch).ToArray();
        _waitingPlayers.RemoveRange(0, _playersPerMatch);

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

            await _producer.ProduceAsync(_completeTopic, message, stoppingToken);

            _logger.LogInformation("Match {MatchId} created with players: {Players}",
                matchId, string.Join(", ", playersForMatch));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send match complete message for match {MatchId}", matchId);
            _waitingPlayers.InsertRange(0, playersForMatch);
        }
    }
}
