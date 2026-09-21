namespace TodoApp.Domain;

public class TodoListShare
{
  public Guid ListId { get; set; }
  public string UserId { get; set; } = default!;

  public TodoList List { get; set; } = null!;
}