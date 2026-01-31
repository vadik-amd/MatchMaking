using Confluent.Kafka;
using System.Text.Json;
using MatchMaking.Service.Services;

namespace MatchMaking.Service.Consumers;

public class MatchCompleteConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MatchCompleteConsumer> _logger;
    private readonly string _completeTopic = "matchmaking.complete";

    public MatchCompleteConsumer(
        IConsumer<string, string> consumer,
        IServiceProvider serviceProvider,
        ILogger<MatchCompleteConsumer> logger)
    {
        _consumer = consumer;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        _consumer.Subscribe(_completeTopic);
        _logger.LogInformation("Started consuming from {Topic}", _completeTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(TimeSpan.FromMilliseconds(100));
                    
                    if (result?.Message?.Value != null)
                    {
                        await ProcessMatchComplete(result.Message.Value);
                        _consumer.Commit(result);
                    }
                }
                catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
                {
                    _logger.LogWarning("Topic {Topic} not yet available, retrying...", _completeTopic);
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
        }
    }

    private async Task ProcessMatchComplete(string messageJson)
    {
        try
        {
            var matchData = JsonSerializer.Deserialize<MatchCompleteMessage>(messageJson);
            
            if (matchData == null)
            {
                _logger.LogWarning("Received null match data");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var matchService = scope.ServiceProvider.GetRequiredService<IMatchMakingService>();
            
            await matchService.SaveMatchInfoAsync(matchData.MatchId, matchData.UserIds);
            
            _logger.LogInformation("Match {MatchId} processed successfully", matchData.MatchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process match complete message");
        }
    }
}

public record MatchCompleteMessage(string MatchId, string[] UserIds);
