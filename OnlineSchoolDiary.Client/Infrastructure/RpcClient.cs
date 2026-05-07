using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using OnlineSchoolDiary.Shared.Protocol;

namespace OnlineSchoolDiary.Client.Infrastructure;

public sealed class RpcClient : IAsyncDisposable
{
    private readonly TcpClient _tcp = new();
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly SemaphoreSlim _sendGate = new(1, 1);

    public string? SessionToken { get; private set; }

    public async Task ConnectAsync(string host, int port)
    {
        await _tcp.ConnectAsync(host, port);
        var stream = _tcp.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
    }

    public void SetSessionToken(string? token) => SessionToken = token;

    public async Task SetSessionTokenOnServerAsync(string token)
    {
        SessionToken = token;
        await SendAsync<object?>("session.setToken", token);
    }

    public async Task<T> SendAsync<T>(string method, object? payload)
    {
        if (_reader is null || _writer is null) throw new InvalidOperationException("Not connected.");
        var requestId = Guid.NewGuid().ToString("N");
        var req = new RpcRequest(method, requestId, payload is null ? null : JsonExt.ToJsonElement(payload));
        var line = JsonSerializer.Serialize(req, JsonDefaults.Options);

        await _sendGate.WaitAsync();
        try
        {
            await _writer.WriteLineAsync(line);
            while (true)
            {
                var respLine = await _reader.ReadLineAsync();
                if (respLine is null) throw new IOException("Disconnected.");
                var resp = JsonSerializer.Deserialize<RpcResponse>(respLine, JsonDefaults.Options);
                if (resp is null) continue;
                if (!string.Equals(resp.RequestId, requestId, StringComparison.Ordinal)) continue;
                if (!resp.Ok) throw new InvalidOperationException(resp.Error ?? "Request failed.");

                if (typeof(T) == typeof(object) || resp.Payload is null)
                    return default!;

                var result = resp.Payload.Value.Deserialize<T>(JsonDefaults.Options);
                return result!;
            }
        }
        finally
        {
            _sendGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _tcp.Close();
        _sendGate.Dispose();
        await Task.CompletedTask;
    }
}

