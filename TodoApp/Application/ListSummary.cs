namespace TodoApp.Application;

public record ListSummary(Guid Id, string Name, DateTime CreatedAt, bool IsShared, int TotalItems, int CompletedItems);
