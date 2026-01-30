var builder = WebApplication.CreateBuilder(args);

// ConfigureServices

var app = builder.Build();

// Configure
app.UseHttpsRedirection();
app.MapControllers();

app.Run();