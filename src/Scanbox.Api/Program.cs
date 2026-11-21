var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(5),
    KeepAliveTimeout = TimeSpan.FromSeconds(5)
});

app.Map("/ws", Scanbox.Api.DeviceWebSocketEndpoint.HandleAsync);
app.UseAuthorization();
app.MapControllers();

app.Run();
