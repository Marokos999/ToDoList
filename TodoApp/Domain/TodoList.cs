namespace TodoApp.Domain;

public class TodoList
{
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Name { get; set; } = string.Empty;
public string OwnerId { get; set; } = string.Empty;
  public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
  public bool IsDeleted { get; set; } = default!;

  public ICollection<TodoItem> Items { get; set; } = [];
  public ICollection<TodoListShare> Shares { get; set; } = [];
}