using TodoApp.Domain;

namespace TodoApp.Application;

public interface ITodoService
{
    Task<List<TodoList>> GetListsForUserAsync(string userId);
    Task<TodoList?> GetListAsync(Guid listId, string userId);
    Task<TodoList> CreateListAsync(string name, string userId);
    Task DeleteListAsync(Guid listId, string userId);

    Task<List<TodoItem>> GetItemsAsync(Guid listId, string userId);
    Task<TodoItem> AddItemAsync(Guid listId, string title, Priority priority, DateTime? dueDate, string userId);
    Task ToggleCompleteAsync(Guid itemId, string userId);
    Task DeleteItemAsync(Guid itemId, string userId);

    Task ShareListAsync(Guid listId, string ownerUserId, string targetEmail);
}