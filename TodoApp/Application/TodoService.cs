using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Hubs;

namespace TodoApp.Application;

public class TodoService(IDbContextFactory<ApplicationDbContext> dbFactory, IHubContext<TodoHub> hub , UserManager<ApplicationUser> userManager) : ITodoService
{
  public async Task<TodoItem?> AddItemAsync(Guid listId, string title, Priority priority, DateTime? dueDate, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    if(!await AccessibleLists(db, userId).AnyAsync(l => l.Id == listId)) return null;

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
    await using var db = await dbFactory.CreateDbContextAsync();
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
    await using var db = await dbFactory.CreateDbContextAsync();
    var item = await AccessibleItems(db, userId).FirstOrDefaultAsync(i => i.Id == itemId);
    if(item is null) return;

    var listId = item.ListId;
    db.TodoItems.Remove(item);
    await db.SaveChangesAsync();
    await hub.Clients.Group(listId.ToString()).SendAsync("TaskChanged");
  }

  public async Task DeleteListAsync(Guid listId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    var list = await db.TodoLists.FirstOrDefaultAsync(l => l.Id == listId && l.OwnerId == userId);
    if(list is null) return;
    list.IsDeleted = true;
    await db.SaveChangesAsync();
    await hub.Clients.Group(listId.ToString()).SendAsync("TaskChanged");
  }

  public async Task<List<TodoItem>> GetItemsAsync(Guid listId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    if(!await AccessibleLists(db, userId).AnyAsync(l => l.Id == listId)) return [];

    return await db.TodoItems
                  .Where(i => i.ListId == listId)
                  .OrderBy(i => i.Order)
                  .ToListAsync();
  }

  public async Task<TodoList?> GetListAsync(Guid listId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    return await AccessibleLists(db, userId)
                  .FirstOrDefaultAsync(l => l.Id == listId);
  }

  public async Task<List<ListSummary>> GetListsForUserAsync(string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    return await AccessibleLists(db, userId)
                  .OrderByDescending(l => l.CreatedAt)
                  .Select(l => new ListSummary(l.Id, l.Name, l.CreatedAt, l.OwnerId != userId,
                                               l.Items.Count, l.Items.Count(i => i.IsCompleted)))
                  .ToListAsync();
  }

  public async Task RenameListAsync(Guid listId, string name, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    var list = await db.TodoLists.FirstOrDefaultAsync(l => l.Id == listId && l.OwnerId == userId);
    if(list is null) return;

    list.Name = name.Trim();
    await db.SaveChangesAsync();
    await hub.Clients.Group(listId.ToString()).SendAsync("TaskChanged");
  }

  public async Task<ShareResult> ShareListAsync(Guid listId, string ownerUserId, string targetEmail)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    var isOwner = await db.TodoLists.AnyAsync(l => l.Id == listId && l.OwnerId == ownerUserId);
    if(!isOwner) return ShareResult.NotOwner;

    var targetUser = await userManager.FindByEmailAsync(targetEmail.Trim());
    if(targetUser is null) return ShareResult.UserNotFound;
    if(targetUser.Id == ownerUserId) return ShareResult.SelfShare;

    var alreadyShared = await db.TodoListShares
                              .AnyAsync(s => s.ListId == listId && s.UserId == targetUser.Id);
    if(alreadyShared) return ShareResult.AlreadyShared;

    db.TodoListShares.Add(new TodoListShare{ListId = listId, UserId = targetUser.Id});

    await db.SaveChangesAsync();
    return ShareResult.Shared;
  }

  public async Task ToggleCompleteAsync(Guid itemId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    var item = await AccessibleItems(db, userId).FirstOrDefaultAsync(i => i.Id == itemId);
    if(item is null) return;

    item.IsCompleted = !item.IsCompleted;
    item.CompletedAt =  item.IsCompleted ? DateTime.UtcNow : null;
    await db.SaveChangesAsync();
    await hub.Clients.Group(item.ListId.ToString()).SendAsync("TaskChanged");
  }

  public async Task UpdateItemAsync(Guid itemId, string title, Priority priority, DateTime? dueDate, string? description, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    var item = await AccessibleItems(db, userId).FirstOrDefaultAsync(i => i.Id == itemId);
    if(item is null) return;

    item.Title = title.Trim();
    item.Priority = priority;
    item.DueDate = dueDate;
    item.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    await db.SaveChangesAsync();
    await hub.Clients.Group(item.ListId.ToString()).SendAsync("TaskChanged");
  }

  private static IQueryable<TodoList> AccessibleLists(ApplicationDbContext db, string userId) =>
    db.TodoLists.Where(l => l.OwnerId == userId || l.Shares.Any(s => s.UserId == userId));

  private static IQueryable<TodoItem> AccessibleItems(ApplicationDbContext db, string userId) =>
    db.TodoItems.Where(i => i.List.OwnerId == userId || i.List.Shares.Any(s => s.UserId == userId));

}