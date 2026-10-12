namespace TodoApp.Application;

public sealed class TodoNotifier(ILogger<TodoNotifier> logger)
{
    public event Func<Guid, string, Task>? ListChanged;

    public void Notify(Guid listId, string changedByUserId = "")
    {
        foreach (var handler in ListChanged?.GetInvocationList().Cast<Func<Guid, string, Task>>() ?? [])
            _ = InvokeAsync(handler, listId, changedByUserId);
    }

    private async Task InvokeAsync(Func<Guid, string, Task> handler, Guid listId, string changedByUserId)
    {
        try
        {
            await handler(listId, changedByUserId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A page failed to refresh after list {ListId} changed", listId);
        }
    }
}
