using TodoApp.Domain;

namespace TodoApp.Application;

public interface ITodoService
{
    public const long MaxAttachmentBytes = 5 * 1024 * 1024;

    Task<List<ListSummary>> GetListsForUserAsync(string userId);
    Task<TodoList?> GetListAsync(Guid listId, string userId);
    Task<TodoList> CreateListAsync(string name, string userId);
    Task DeleteListAsync(Guid listId, string userId);
    Task RenameListAsync(Guid listId, string name, string userId);

    Task<List<TodoItem>> GetItemsAsync(Guid listId, string userId);
    Task<TodoItem?> AddItemAsync(Guid listId, string title, Priority priority, DateTime? dueDate, string userId);
    Task ToggleCompleteAsync(Guid itemId, string userId);
    Task DeleteItemAsync(Guid itemId, string userId);
    Task UpdateItemAsync(Guid itemId, string title, Priority priority, DateTime? dueDate, string? description, string userId);

    Task MoveItemAsync(Guid itemId, Guid targetItemId, string userId);

    Task<List<AttachmentInfo>> GetAttachmentsAsync(Guid itemId, string userId);
    Task<TodoAttachment?> GetAttachmentAsync(Guid attachmentId, string userId);
    Task<bool> AddAttachmentAsync(Guid itemId, string fileName, string contentType, Stream content, string userId);
    Task DeleteAttachmentAsync(Guid attachmentId, string userId);

    Task<Dictionary<Guid, int>> GetAttachmentCountsAsync(Guid listId, string userId);
    Task<string?> ExportCsvAsync(Guid listId, string userId);
    /// <returns>Number of imported tasks, or null if the list is inaccessible or the file is not a valid export.</returns>
    Task<int?> ImportCsvAsync(Guid listId, string csv, string userId);

    /// <summary>Permanently removes everything the user owns (including soft-deleted lists) and their access to shared lists.</summary>
    Task DeleteUserDataAsync(string userId);

    Task<ShareResult> ShareListAsync(Guid listId, string ownerUserId, string targetEmail);
}