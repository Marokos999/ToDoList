using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Hubs;

namespace TodoApp.Application;

public class TodoService(ApplicationDbContext db, IHubContext<TodoHub> hub , UserManager<ApplicationUser> userManager) : ITodoService
{
  public async Task<TodoItem> AddItemAsync(Guid listId, string title, Priority priority, DateTime? dueDate, string userId)
  {
    var maxOrder = await db.TodoItems
                          .Where(i => i.ListId == listId)
                          .MaxAsync(i => (int?)i.Order) ?? 0;
    var item = new TodoItem
    {
      ListId = listId,
      Title = title,
      Priority = priority,
      DueDate = dueDate,
      Order = maxOrder + 1
    };

    db.TodoItems.Add(item);
    await db.SaveChangesAsync();
    await hub.Clients.Group(listId.ToString()).SendAsync("TaskChanged");
    return item;
  }

  public async Task<TodoList> CreateListAsync(string name, string userId)
  {
    var list = new TodoList
    {
      Name = name,
       OwnerId = userId
    };

    db.TodoLists.Add(list);
    await db.SaveChangesAsync();
    return list;
  }

  public async Task DeleteItemAsync(Guid itemId, string userId)
  {
    var item = await db.TodoItems.FindAsync(itemId);
    if(item is null) return;

    var listId = item.ListId;
    db.TodoItems.Remove(item);
    await db.SaveChangesAsync();
    await hub.Clients.Group(listId.ToString()).SendAsync("TaskChanged");
  }

  public async Task DeleteListAsync(Guid listId, string userId)
  {
    var list = await db.TodoLists.FirstOrDefaultAsync(l => l.Id == listId && l.OwnerId == userId);
    if(list is null) return;
    list.IsDeleted = true;
    await db.SaveChangesAsync();
  }

  public async Task<List<TodoItem>> GetItemsAsync(Guid listId, string userId)
  {
    var hasAccess = await db.TodoLists.AnyAsync(l => l.Id == listId && (l.OwnerId == userId ||
                                                l.Shares.Any(s => s.UserId == userId)));

    if(!hasAccess) return [];

    return await db.TodoItems
                  .Where(i => i.ListId == listId)
                  .OrderBy(i => i.Order)
                  .ToListAsync();
  }

  public async Task<TodoList?> GetListAsync(Guid listId, string userId)
  {
    return await db.TodoLists
                  .Include(l => l.Items.OrderBy(i => i.Order))
                  .FirstOrDefaultAsync(l => l.Id == listId && (l.OwnerId == userId || l.Shares.Any(s => s.UserId == userId)));
  }

  public async Task<List<TodoList>> GetListsForUserAsync(string userId)
  {
    var owner = await db.TodoLists
                        .Where(i => i.OwnerId == userId)
                        .ToListAsync();

    var share = await db.TodoLists
                        .Where(s => s.Shares.Any(a => a.UserId == userId))
                        .ToListAsync();


    return owner.Union(share).OrderByDescending(l => l.CreatedAt).ToList();
  }

  public async Task ShareListAsync(Guid listId, string ownerUserId, string targetEmail)
  {
    var list = await db.TodoLists.FirstOrDefaultAsync(l => l.Id == listId && l.OwnerId == ownerUserId);
    if(list is null) return;

    var targerUser = await userManager.FindByEmailAsync(targetEmail);
    if(targerUser is null) return;

    var alreadyShare = await db.TodoListShares
                              .AnyAsync(s => s.ListId == listId && s.UserId == targerUser.Id);
    if(alreadyShare) return;

    db.TodoListShares.Add(new TodoListShare{ListId = listId, UserId = targerUser.Id});

    await db.SaveChangesAsync();
  }

  public async Task ToggleCompleteAsync(Guid itemId, string userId)
  {
    var item = await db.TodoItems.FindAsync(itemId);
    if(item is null) return;

    item.IsCompleted = !item.IsCompleted;
    item.CompletedAt =  item.IsCompleted ? DateTime.UtcNow : null;
    await db.SaveChangesAsync();
    await hub.Clients.Group(item.ListId.ToString()).SendAsync("TaskChanged");
  }

}