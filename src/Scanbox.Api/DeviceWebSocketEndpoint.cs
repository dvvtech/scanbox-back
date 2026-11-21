using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Scanbox.Api;

public static class DeviceWebSocketEndpoint
{
    private static readonly ConcurrentDictionary<string, Peer> Devices = new();
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Peer>> Browsers = new();
    private static readonly ConcurrentDictionary<string, FailureWindow> PairingFailures = new();
    private static readonly TimeSpan PairingFailureWindow = TimeSpan.FromMinutes(5);
    private const int MaxPairingFailures = 5;

    public static async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        string role = context.Request.Query["role"].ToString().Trim().ToLowerInvariant();
        string mac = NormalizeMac(context.Request.Query["mac"].ToString());
        string token = NormalizeToken(context.Request.Query["token"].ToString());
        if (mac.Length == 0 || token.Length == 0 || (role != "device" && role != "web"))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Expected role=device|web, a valid mac, and a 5-digit pairing code.");
            return;
        }

        if (role == "web" && !IsAllowedBrowserOrigin(context.Request.Headers["Origin"].ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync();
        var peer = new Peer(socket, token);
        CancellationToken cancellationToken = context.RequestAborted;

        if (role == "device")
        {
            Peer? previous = Devices.AddOrUpdate(mac, peer, (_, _) => peer);
            if (!ReferenceEquals(previous, peer))
            {
                Console.WriteLine("[WS] Replaced existing device connection for " + mac);
                await CloseQuietlyAsync(previous);
            }

            Console.WriteLine("[WS] Device connected: " + mac);
            await SendDeviceStatusToBrowsersAsync(mac, token, true, cancellationToken);
        }
        else
        {
            ConcurrentDictionary<Guid, Peer> peers = Browsers.GetOrAdd(mac, _ => new ConcurrentDictionary<Guid, Peer>());
            peers[peer.Id] = peer;
            Console.WriteLine("[WS] Browser connected for device: " + mac);
            bool online = Devices.TryGetValue(mac, out Peer? device)
                && device.Socket.State == WebSocketState.Open
                && TokensMatch(device.Token, token);
            await peer.SendTextAsync(JsonSerializer.Serialize(new
            {
                type = "deviceStatus",
                online
            }), cancellationToken);
        }

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                string? message = await ReceiveTextAsync(socket, cancellationToken);
                if (message is null) break;

                if (role == "device")
                {
                    Console.WriteLine("[WS] Device " + mac + " sent: " + message);
                    await BroadcastToBrowsersAsync(mac, token, message, cancellationToken);
                }
                else
                {
                    await HandleBrowserMessageAsync(mac, token, peer, message, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine("[WS] " + role + " connection ended for " + mac + ": " + ex.Message);
        }
        finally
        {
            if (role == "device")
            {
                if (((ICollection<KeyValuePair<string, Peer>>)Devices).Remove(new KeyValuePair<string, Peer>(mac, peer)))
                {
                    Console.WriteLine("[WS] Device disconnected: " + mac);
                    await SendDeviceStatusToBrowsersAsync(mac, token, false, CancellationToken.None);
                }
            }
            else if (Browsers.TryGetValue(mac, out ConcurrentDictionary<Guid, Peer>? peers))
            {
                peers.TryRemove(peer.Id, out _);
                if (peers.IsEmpty)
                {
                    ((ICollection<KeyValuePair<string, ConcurrentDictionary<Guid, Peer>>>)Browsers)
                        .Remove(new KeyValuePair<string, ConcurrentDictionary<Guid, Peer>>(mac, peers));
                }
                Console.WriteLine("[WS] Browser disconnected for device: " + mac);
            }

            await CloseQuietlyAsync(peer);
        }
    }

    private static async Task HandleBrowserMessageAsync(string mac, string token, Peer browser, string message, CancellationToken cancellationToken)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(message);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out JsonElement type) || type.GetString() != "scan")
            {
                await browser.SendTextAsync(JsonSerializer.Serialize(new
                {
                    type = "error",
                    message = "Unsupported command."
                }), cancellationToken);
                return;
            }

            string requestId = root.TryGetProperty("requestId", out JsonElement id)
                ? id.GetString() ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N");

            if (!Devices.TryGetValue(mac, out Peer? device) || device.Socket.State != WebSocketState.Open)
            {
                await browser.SendTextAsync(JsonSerializer.Serialize(new
                {
                    type = "deviceOffline",
                    mac
                }), cancellationToken);
                return;
            }

            if (IsPairingLocked(mac))
            {
                await browser.SendTextAsync(JsonSerializer.Serialize(new
                {
                    type = "error",
                    message = "Слишком много неверных кодов. Попробуйте ещё раз через 5 минут."
                }), cancellationToken);
                return;
            }

            if (!TokensMatch(device.Token, token))
            {
                RecordPairingFailure(mac);
                await browser.SendTextAsync(JsonSerializer.Serialize(new
                {
                    type = "error",
                    message = "Код сопряжения не подходит к этому устройству."
                }), cancellationToken);
                return;
            }

            ClearPairingFailures(mac);

            string command = JsonSerializer.Serialize(new
            {
                type = "scan",
                requestId
            });
            await device.SendTextAsync(command, cancellationToken);
            await browser.SendTextAsync(JsonSerializer.Serialize(new
            {
                type = "scanQueued",
                requestId
            }), cancellationToken);
            Console.WriteLine("[WS] Scan request routed to " + mac + " (request " + requestId + ").");
        }
        catch (JsonException)
        {
            await browser.SendTextAsync(JsonSerializer.Serialize(new
            {
                type = "error",
                message = "Invalid JSON message."
            }), cancellationToken);
        }
    }

    private static async Task BroadcastToBrowsersAsync(string mac, string deviceToken, string message, CancellationToken cancellationToken)
    {
        if (!Browsers.TryGetValue(mac, out ConcurrentDictionary<Guid, Peer>? peers)) return;

        foreach (Peer browser in peers.Values)
        {
            if (!TokensMatch(deviceToken, browser.Token)) continue;
            try
            {
                await browser.SendTextAsync(message, cancellationToken);
            }
            catch (WebSocketException)
            {
            }
        }
    }

    private static Task SendDeviceStatusToBrowsersAsync(string mac, string deviceToken, bool online, CancellationToken cancellationToken)
    {
        string message = JsonSerializer.Serialize(new
        {
            type = "deviceStatus",
            online
        });
        return BroadcastToBrowsersAsync(mac, deviceToken, message, cancellationToken);
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[2048];
        using var message = new MemoryStream();

        while (true)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (result.MessageType != WebSocketMessageType.Text) return null;

            message.Write(buffer, 0, result.Count);
            if (message.Length > 8192) return null;
            if (result.EndOfMessage) break;
        }

        return Encoding.UTF8.GetString(message.ToArray());
    }

    private static bool IsPairingLocked(string mac)
    {
        return PairingFailures.TryGetValue(mac, out FailureWindow? window)
            && window.IsLocked(DateTimeOffset.UtcNow);
    }

    private static void RecordPairingFailure(string mac)
    {
        PairingFailures.GetOrAdd(mac, _ => new FailureWindow()).Record(DateTimeOffset.UtcNow);
    }

    private static void ClearPairingFailures(string mac)
    {
        if (PairingFailures.TryGetValue(mac, out FailureWindow? window))
        {
            window.Clear();
        }
    }

    private sealed class FailureWindow
    {
        private readonly object _sync = new();
        private readonly Queue<DateTimeOffset> _failures = new();

        public void Record(DateTimeOffset now)
        {
            lock (_sync)
            {
                RemoveExpired(now);
                _failures.Enqueue(now);
            }
        }

        public bool IsLocked(DateTimeOffset now)
        {
            lock (_sync)
            {
                RemoveExpired(now);
                return _failures.Count >= MaxPairingFailures;
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _failures.Clear();
            }
        }

        private void RemoveExpired(DateTimeOffset now)
        {
            while (_failures.Count > 0 && now - _failures.Peek() >= PairingFailureWindow)
            {
                _failures.Dequeue();
            }
        }
    }

    private static string NormalizeMac(string value)
    {
        string normalized = value.Trim().Replace('-', ':').ToUpperInvariant();
        return Regex.IsMatch(normalized, "^([0-9A-F]{2}:){5}[0-9A-F]{2}$") ? normalized : string.Empty;
    }

    private static string NormalizeToken(string value)
    {
        string normalized = value.Trim().ToUpperInvariant();
        return Regex.IsMatch(normalized, "^[0-9]{5}$") ? normalized : string.Empty;
    }

    private static bool TokensMatch(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
    }

    private static bool IsAllowedBrowserOrigin(string origin)
    {
        return origin == "https://scanbox.tech"
            || origin == "https://www.scanbox.tech"
            || origin == "https://dvvtech.github.io"
            || origin == "http://localhost:8080"
            || origin == "http://127.0.0.1:8080";
    }

    private static async Task CloseQuietlyAsync(Peer peer)
    {
        try
        {
            if (peer.Socket.State == WebSocketState.Open || peer.Socket.State == WebSocketState.CloseReceived)
            {
                await peer.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
        }
    }

    private sealed class Peer
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public Peer(WebSocket socket, string token)
        {
            Socket = socket;
            Token = token;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public WebSocket Socket { get; }
        public string Token { get; }

        public async Task SendTextAsync(string message, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                if (Socket.State == WebSocketState.Open)
                {
                    await Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}

