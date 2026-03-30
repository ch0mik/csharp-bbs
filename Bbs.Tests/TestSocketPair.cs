using System.Net;
using System.Net.Sockets;

namespace Bbs.Tests;

internal static class TestSocketPair
{
    public static async Task<(TcpClient Server, TcpClient Client)> CreateAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var client = new TcpClient();
        var connectTask = client.ConnectAsync(IPAddress.Loopback, port);
        var server = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
        await connectTask.ConfigureAwait(false);

        listener.Stop();
        return (server, client);
    }
}
