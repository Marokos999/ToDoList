using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;

namespace TodoApp.Application;

public class TodoService(IDbContextFactory<ApplicationDbContext> dbFactory, TodoNotifier notifier,UserManager<ApplicationUser> userManager) : ITodoService
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
    notifier.Notify(listId);
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
    notifier.Notify(listId);
  }

  public async Task DeleteListAsync(Guid listId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    var list = await db.TodoLists.FirstOrDefaultAsync(l => l.Id == listId && l.OwnerId == userId);
    if(list is null) return;
    list.IsDeleted = true;
    await db.SaveChangesAsync();
    notifier.Notify(listId);
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
    notifier.Notify(listId);
  }

  public async Task MoveItemAsync(Guid itemId, Guid targetItemId, string userId)
  {
    if(itemId == targetItemId) return;

    await using var db = await dbFactory.CreateDbContextAsync();
    var moved = await AccessibleItems(db, userId).FirstOrDefaultAsync(i => i.Id == itemId);
    if(moved is null) return;

    var items = await db.TodoItems
                       .Where(i => i.ListId == moved.ListId)
                       .OrderBy(i => i.Order)
                       .ToListAsync();
    var targetIndex = items.FindIndex(i => i.Id == targetItemId);
    if(targetIndex < 0) return;

    items.Remove(moved);
    items.Insert(targetIndex, moved);
    for(var i = 0; i < items.Count; i++)
      items[i].Order = i + 1;

    await db.SaveChangesAsync();
    notifier.Notify(moved.ListId);
  }

  public async Task<List<AttachmentInfo>> GetAttachmentsAsync(Guid itemId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    if(!await AccessibleItems(db, userId).AnyAsync(i => i.Id == itemId)) return [];

    return await db.TodoAttachments
                  .Where(a => a.ItemId == itemId)
                  .OrderBy(a => a.CreatedAt)
                  .Select(a => new AttachmentInfo(a.Id, a.FileName, a.Size))
                  .ToListAsync();
  }

  public async Task<TodoAttachment?> GetAttachmentAsync(Guid attachmentId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    return await db.TodoAttachments
                  .Where(a => a.Id == attachmentId && AccessibleItems(db, userId).Any(i => i.Id == a.ItemId))
                  .FirstOrDefaultAsync();
  }

  public async Task<bool> AddAttachmentAsync(Guid itemId, string fileName, string contentType, Stream content, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    var item = await AccessibleItems(db, userId).FirstOrDefaultAsync(i => i.Id == itemId);
    if(item is null) return false;

    // The size cap is also enforced by the uploader; this is the final guard
    using var buffer = new MemoryStream();
    await content.CopyToAsync(buffer);
    if(buffer.Length > ITodoService.MaxAttachmentBytes || buffer.Length == 0) return false;

    db.TodoAttachments.Add(new TodoAttachment
    {
      ItemId = itemId,
      FileName = Path.GetFileName(fileName),
      ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
      Size = buffer.Length,
      Data = buffer.ToArray()
    });
    await db.SaveChangesAsync();
    notifier.Notify(item.ListId);
    return true;
  }

  public async Task DeleteAttachmentAsync(Guid attachmentId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    var attachment = await db.TodoAttachments
                            .Include(a => a.Item)
                            .Where(a => a.Id == attachmentId && AccessibleItems(db, userId).Any(i => i.Id == a.ItemId))
                            .FirstOrDefaultAsync();
    if(attachment is null) return;

    var listId = attachment.Item.ListId;
    db.TodoAttachments.Remove(attachment);
    await db.SaveChangesAsync();
    notifier.Notify(listId);
  }

  public async Task<Dictionary<Guid, int>> GetAttachmentCountsAsync(Guid listId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    if(!await AccessibleLists(db, userId).AnyAsync(l => l.Id == listId)) return [];

    return await db.TodoAttachments
                  .Where(a => a.Item.ListId == listId)
                  .GroupBy(a => a.ItemId)
                  .ToDictionaryAsync(g => g.Key, g => g.Count());
  }

  public async Task<string?> ExportCsvAsync(Guid listId, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    if(!await AccessibleLists(db, userId).AnyAsync(l => l.Id == listId)) return null;

    var items = await db.TodoItems.Where(i => i.ListId == listId).OrderBy(i => i.Order).ToListAsync();
    return TodoCsv.Write(items);
  }

  public async Task<int?> ImportCsvAsync(Guid listId, string csv, string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    if(!await AccessibleLists(db, userId).AnyAsync(l => l.Id == listId)) return null;

    var tasks = TodoCsv.Read(csv);
    if(tasks is null) return null;

    var order = await db.TodoItems.Where(i => i.ListId == listId).MaxAsync(i => (int?)i.Order) ?? 0;
    foreach(var t in tasks)
      db.TodoItems.Add(new TodoItem
      {
        ListId = listId,
        Title = t.Title,
        Description = t.Description,
        Priority = t.Priority,
        DueDate = t.DueDate,
        IsCompleted = t.IsCompleted,
        CompletedAt = t.IsCompleted ? DateTime.UtcNow : null,
        Order = ++order
      });

    await db.SaveChangesAsync();
    notifier.Notify(listId);
    return tasks.Count;
  }

  public async Task DeleteUserDataAsync(string userId)
  {
    await using var db = await dbFactory.CreateDbContextAsync();
    // Items, attachments and shares of the owned lists go with them through the cascading foreign keys
    var listIds = await db.TodoLists.IgnoreQueryFilters().Where(l => l.OwnerId == userId).Select(l => l.Id).ToListAsync();
    await db.TodoLists.IgnoreQueryFilters().Where(l => l.OwnerId == userId).ExecuteDeleteAsync();
    await db.TodoListShares.Where(s => s.UserId == userId).ExecuteDeleteAsync();
    foreach(var id in listIds) notifier.Notify(id);
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
    notifier.Notify(item.ListId);
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
    notifier.Notify(item.ListId);
  }

  private static IQueryable<TodoList> AccessibleLists(ApplicationDbContext db, string userId) =>
    db.TodoLists.Where(l => l.OwnerId == userId || l.Shares.Any(s => s.UserId == userId));

  private static IQueryable<TodoItem> AccessibleItems(ApplicationDbContext db, string userId) =>
    db.TodoItems.Where(i => i.List.OwnerId == userId || i.List.Shares.Any(s => s.UserId == userId));

}