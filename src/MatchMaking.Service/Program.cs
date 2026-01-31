using Confluent.Kafka;
using MatchMaking.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// ConfigureServices
builder.Services.AddControllers();
builder.Services.AddStackExchangeRedisCache(options => 
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

builder.Services.AddSingleton<IProducer<string, string>>(sp =>
{
    var config = new ProducerConfig
    {
        BootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092"
    };
    return new ProducerBuilder<string, string>(config).Build();
});

builder.Services.AddSingleton<IMatchMakingService, MatchMakingService>();
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