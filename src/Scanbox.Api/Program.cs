using Microsoft.EntityFrameworkCore;
using Scanbox.Api.DAL;
using Scanbox.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<ISchoolDataStore, InMemorySchoolDataStore>();
string connectionString = builder.Configuration.GetConnectionString("ScanboxDb")
    ?? throw new InvalidOperationException("Connection string 'ScanboxDb' is not configured.");
builder.Services.AddDbContextFactory<ScanboxDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IDeviceStateRepository, DeviceStateRepository>();

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
