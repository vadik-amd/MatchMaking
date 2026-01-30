var builder = Host.CreateApplicationBuilder(args);

// ConfigureServices

var host = builder.Build();
host.Run();