using Microsoft.EntityFrameworkCore;
using Scanbox.Api.DAL;
using Scanbox.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<ISchoolDataStore, InMemorySchoolDataStore>();
string connectionString = ResolveConnectionString(builder.Configuration);
builder.Services.AddDbContextFactory<ScanboxDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IDeviceStateRepository, DeviceStateRepository>();

var app = builder.Build();

try
{
    await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
    {
        ScanboxDbContext db = scope.ServiceProvider.GetRequiredService<ScanboxDbContext>();
        await db.Database.MigrateAsync();
        Console.WriteLine($"Success migration");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Critical error during migration: {ex.Message}");
    throw;
}

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

static string ResolveConnectionString(IConfiguration configuration)
{
    const string dockerSecretName = "scanbox_connection_string";
    const string secretPath = "/run/secrets/" + dockerSecretName;

    try
    {
        string secretConnectionString = File.ReadAllText(secretPath).Trim();
        if (secretConnectionString.Length == 0)
        {
            throw new InvalidOperationException($"Docker secret '{dockerSecretName}' is empty.");
        }

        Console.WriteLine("[DB] Connection string loaded from Docker secret: " + secretPath);
        return secretConnectionString;
    }
    catch (FileNotFoundException)
    {
    }
    catch (DirectoryNotFoundException)
    {
    }

    string? configuredConnectionString = configuration.GetConnectionString("ScanboxDb");
    if (!string.IsNullOrWhiteSpace(configuredConnectionString))
    {
        return configuredConnectionString;
    }

    throw new InvalidOperationException(
        $"Connection string is not configured. Add the Docker secret '{dockerSecretName}' or set ConnectionStrings:ScanboxDb.");
}
