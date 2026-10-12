using TodoApp.Domain;

namespace TodoApp.Application;

public record DashboardData(
    List<TodoItem> DueToday,
    List<TodoItem> DueThisWeek,
    List<TodoItem> Overdue,
    int TotalActive);
