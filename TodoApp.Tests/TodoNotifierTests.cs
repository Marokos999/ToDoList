using Microsoft.Extensions.Logging.Abstractions;
using TodoApp.Application;

namespace TodoApp.Tests;

public sealed class TodoNotifierTests
{
    [Fact]
    public void Notify_StillReachesOtherSubscribers_WhenOneThrows()
    {
        var notifier = new TodoNotifier(NullLogger<TodoNotifier>.Instance);
        var reached = false;
        notifier.ListChanged += _ => throw new InvalidOperationException();
        notifier.ListChanged += _ =>
        {
            reached = true;
            return Task.CompletedTask;
        };

        notifier.Notify(Guid.NewGuid());

        Assert.True(reached);
    }
}
