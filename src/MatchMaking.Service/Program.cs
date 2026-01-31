using Confluent.Kafka;
using MatchMaking.Service.Consumers;
using MatchMaking.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// ConfigureServices
builder.Services.AddControllers();
builder.Services.AddStackExchangeRedisCache(options => 
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

builder.Services.AddSingleton<IProducer<string, string>>(_ =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"],
        AllowAutoCreateTopics = builder.Configuration.GetValue("Kafka:AllowAutoCreateTopics", true)
    };
    return new ProducerBuilder<string, string>(config).Build();
});
builder.Services.AddSingleton<IConsumer<string, string>>(_ =>
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

builder.Services.AddSingleton<IMatchMakingService, MatchMakingService>();
builder.Services.AddHostedService<MatchCompleteConsumer>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.MapControllers();

app.Run();