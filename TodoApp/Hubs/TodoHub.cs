using Microsoft.AspNetCore.SignalR;

namespace TodoApp.Hubs;

public class TodoHub : Hub
{
  public Task JoinListAsync(string listId) => Groups.AddToGroupAsync(Context.ConnectionId, listId);

  public Task LeaveListAsync(string listId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, listId);

}