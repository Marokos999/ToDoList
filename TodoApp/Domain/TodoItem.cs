namespace TodoApp.Domain;

public class TodoItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ListId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Order { get; set; }

    public TodoList List { get; set; } = null!;
}