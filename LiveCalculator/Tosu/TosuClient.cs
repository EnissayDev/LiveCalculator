using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LiveCalculator.Tosu;

/// <summary>
/// Connects to a running tosu instance and streams normalised <see cref="LiveSnapshot"/> updates.
/// Reconnects automatically while running.
/// </summary>
public class TosuClient : IDisposable
{
    public const string DefaultUri = "ws://127.0.0.1:24050/websocket/v2";

    private static readonly JsonSerializerOptions json_options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly Uri uri;
    private CancellationTokenSource? cts;

    /// <summary>Fired on every successfully-parsed update (on a background thread).</summary>
    public event Action<LiveSnapshot>? SnapshotReceived;

    /// <summary>Fired when the connection state changes. Argument is true when connected.</summary>
    public event Action<bool, string>? ConnectionChanged;

    public TosuClient(string? uri = null)
    {
        this.uri = new Uri(uri ?? DefaultUri);
    }

    public void Start()
    {
        Stop();
        cts = new CancellationTokenSource();
        _ = Task.Run(() => runLoop(cts.Token));
    }

    public void Stop()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private async Task runLoop(CancellationToken token)
    {
        var buffer = new byte[1 << 20];

        while (!token.IsCancellationRequested)
        {
            using var socket = new ClientWebSocket();

            try
            {
                ConnectionChanged?.Invoke(false, "Connecting to tosu…");
                await socket.ConnectAsync(uri, token).ConfigureAwait(false);
                ConnectionChanged?.Invoke(true, "Connected to tosu");

                while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var message = await receiveMessage(socket, buffer, token).ConfigureAwait(false);
                    if (message == null)
                        break;

                    try
                    {
                        var payload = JsonSerializer.Deserialize<TosuPayload>(message, json_options);
                        if (payload != null)
                            SnapshotReceived?.Invoke(LiveSnapshot.FromPayload(payload));
                    }
                    catch (JsonException)
                    {
                        // Ignore malformed frames — tosu occasionally emits partial state.
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

    private static async Task<string?> receiveMessage(ClientWebSocket socket, byte[] buffer, CancellationToken token)
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
