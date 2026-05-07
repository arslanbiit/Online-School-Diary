using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using OnlineSchoolDiary.Shared.Protocol;

namespace OnlineSchoolDiary.Server.Rpc;

public sealed class TcpRpcServer
{
    private readonly TcpListener _listener;
    private readonly RpcMethods _methods;

    public TcpRpcServer(IPAddress ip, int port, RpcMethods methods)
    {
        _listener = new TcpListener(ip, port);
        _methods = methods;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        Console.WriteLine($"OnlineSchoolDiary.Server listening on {_listener.LocalEndpoint}");

        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        await using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
        using (var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true })
        {
            string? sessionToken = null;

            while (!ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                RpcRequest? req;
                try
                {
                    req = JsonSerializer.Deserialize<RpcRequest>(line, JsonDefaults.Options);
                }
                catch
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new RpcResponse("?", false, "Invalid request.", null), JsonDefaults.Options));
                    continue;
                }

                if (req is null)
                {
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new RpcResponse("?", false, "Invalid request.", null), JsonDefaults.Options));
                    continue;
                }

                // Special: allow clients to set sessionToken after login.
                if (req.Method == "session.setToken" && req.Payload is not null)
                {
                    var token = req.Payload.Value.GetString();
                    sessionToken = token;
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new RpcResponse(req.RequestId, true, null, null), JsonDefaults.Options));
                    continue;
                }

                var resp = await _methods.HandleAsync(req, sessionToken);
                await writer.WriteLineAsync(JsonSerializer.Serialize(resp, JsonDefaults.Options));
            }
        }
    }
}

