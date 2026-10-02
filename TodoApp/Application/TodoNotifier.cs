namespace TodoApp.Application;

public sealed class TodoNotifier(ILogger<TodoNotifier> logger)
{
    public event Func<Guid, Task>? ListChanged;

    // Subscribers are other users' pages: the caller neither waits for them nor fails because of them
    public void Notify(Guid listId)
    {
        foreach (var handler in ListChanged?.GetInvocationList().Cast<Func<Guid, Task>>() ?? [])
            _ = InvokeAsync(handler, listId);
    }

    private async Task InvokeAsync(Func<Guid, Task> handler, Guid listId)
    {
        try
        {
            await handler(listId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A page failed to refresh after list {ListId} changed", listId);
        }
    }
}
