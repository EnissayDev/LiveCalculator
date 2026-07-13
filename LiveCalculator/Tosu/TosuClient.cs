using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiveCalculator.Tosu;

public class TosuClient : IDisposable
{
    public const string DefaultUri = "ws://127.0.0.1:24050/websocket/v2";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly Uri _uri;
    private CancellationTokenSource? _cts;
    private DateTime _lastRawWrite = DateTime.MinValue;

    public string? DebugLogPath { get; set; }

    public event Action<LiveSnapshot>? SnapshotReceived;

    public event Action<bool, string>? ConnectionChanged;

    public TosuClient(string? uri = null)
    {
        _uri = new Uri(uri ?? DefaultUri);
    }

    public void Start()
    {
        Stop();
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => RunLoop(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunLoop(CancellationToken token)
    {
        var buffer = new byte[1 << 20];

        while (!token.IsCancellationRequested)
        {
            using var socket = new ClientWebSocket();

            try
            {
                ConnectionChanged?.Invoke(false, "Connecting to tosu…");
                await socket.ConnectAsync(_uri, token).ConfigureAwait(false);
                ConnectionChanged?.Invoke(true, "Connected to tosu");

                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var message = await ReceiveMessage(socket, buffer, token).ConfigureAwait(false);
                    if (message == null)
                        break;

                    WriteDebug(message);

                    try
                    {
                        var payload = JsonSerializer.Deserialize<TosuPayload>(message, JsonOptions);
                        if (payload != null)
                            SnapshotReceived?.Invoke(LiveSnapshot.FromPayload(payload));
                    }
                    catch (JsonException)
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ConnectionChanged?.Invoke(false, $"tosu not reachable ({ex.GetType().Name}) — retrying…");
            }

            if (token.IsCancellationRequested)
                break;

            try
            {
                await Task.Delay(2000, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void WriteDebug(string message)
    {
        if (string.IsNullOrEmpty(DebugLogPath))
            return;

        if ((DateTime.UtcNow - _lastRawWrite).TotalSeconds < 1)
            return;

        _lastRawWrite = DateTime.UtcNow;

        try
        {
            System.IO.File.WriteAllText(DebugLogPath, message);
        }
        catch
        {
        }
    }

    private static async Task<string?> ReceiveMessage(ClientWebSocket socket, byte[] buffer, CancellationToken token)
    {
        var sb = new StringBuilder();
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        } while (!result.EndOfMessage);

        return sb.ToString();
    }

    public void Dispose() => Stop();
}
