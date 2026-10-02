using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TodoApp.Hubs;

namespace TodoApp.Tests;

public sealed class FakeHubContext : IHubContext<TodoHub>
{
    private readonly FakeClients clients = new();

    public IHubClients Clients => clients;
    public IGroupManager Groups => throw new NotSupportedException();

    public IReadOnlyList<(string Group, string Method)> Sent => clients.Sent.ToList();

    private sealed class FakeClients : IHubClients
    {
        public ConcurrentQueue<(string Group, string Method)> Sent { get; } = new();

        public IClientProxy Group(string groupName) => new RecordingProxy(this, groupName);

        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class RecordingProxy(FakeClients clients, string group) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            clients.Sent.Enqueue((group, method));
            return Task.CompletedTask;
        }
    }
}
