using Confluent.Kafka;
using MatchMaking.Worker;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IConsumer<string, string>>(sp =>
{
    var config = new ConsumerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        GroupId = builder.Configuration["Kafka:GroupId"],
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false
    };
    return new ConsumerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        AllowAutoCreateTopics = builder.Configuration.GetValue<bool>("Kafka:AllowAutoCreateTopics", true)
    };
    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("Redis");
    return ConnectionMultiplexer.Connect(connectionString!);
});

builder.Services.AddHostedService<MatchMakingWorker>();

var host = builder.Build();
host.Run();