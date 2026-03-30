using Bbs.Core;
using Bbs.Terminals;
using System.Net.Sockets;

namespace Bbs.Tests;

public class BbsThreadTests
{
    private sealed class DummyThread : PetsciiThread
    {
        public override Task DoLoopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void SetIdentity(long id, string name)
        {
            ClientId = id;
            ClientName = name;
            ClientClass = GetType();
            ServerPort = 6510;
        }
    }

    [Fact]
    public void ChangeClientName_ShouldRejectEmptyAndConflictingNames()
    {
        BbsThread.Clients.Clear();

        var t1 = new DummyThread();
        t1.SetIdentity(1, "alice");
        var t2 = new DummyThread();
        t2.SetIdentity(2, "bob");

        BbsThread.Clients[1] = t1;
        BbsThread.Clients[2] = t2;

        Assert.Equal(-1, t2.ChangeClientName("   "));
        Assert.Equal(-2, t2.ChangeClientName("alice"));
        Assert.Equal(0, t2.ChangeClientName("charlie"));
        Assert.Equal("charlie", t2.ClientName);

        BbsThread.Clients.Clear();
    }

    [Fact]
    public void BuildDiagnosticsHtml_ShouldContainClientCount()
    {
        BbsThread.Clients.Clear();

        var t1 = new DummyThread();
        t1.SetIdentity(1, "alice");
        BbsThread.Clients[1] = t1;

        var html = BbsThread.BuildDiagnosticsHtml();

        Assert.Contains("HTTP/1.1 200 OK", html, StringComparison.Ordinal);
        Assert.Contains("Number of clients: 1", html, StringComparison.Ordinal);
        Assert.Contains("alice", html, StringComparison.Ordinal);

        BbsThread.Clients.Clear();
    }
}

