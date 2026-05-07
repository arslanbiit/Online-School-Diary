using System.Net;
using OnlineSchoolDiary.Server.Infrastructure;
using OnlineSchoolDiary.Server.Rpc;
using OnlineSchoolDiary.Server.Services;

var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
var store = new JsonFileStore(dataDir);
var state = new AppState();
var data = new DataService(store, state);
await data.InitializeAsync();

var sessions = new SessionManager();
var methods = new RpcMethods(data, sessions);
var server = new TcpRpcServer(IPAddress.Loopback, port: 5050, methods);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

await server.RunAsync(cts.Token);
