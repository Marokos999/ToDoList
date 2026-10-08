using System.Globalization;
using System.Text;
using TodoApp.Domain;

namespace TodoApp.Application;

public record CsvTask(string Title, string? Description, Priority Priority, DateTime? DueDate, bool IsCompleted);

public static class TodoCsv
{
  private const string Header = "Title,Description,Priority,DueDate,Completed";

  public static string Write(IEnumerable<TodoItem> items)
  {
    var sb = new StringBuilder(Header).Append("\r\n");
    foreach(var i in items)
      sb.Append(string.Join(',',
          Escape(i.Title),
          Escape(i.Description ?? string.Empty),
          i.Priority,
          i.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
          i.IsCompleted ? "true" : "false"))
        .Append("\r\n");
    return sb.ToString();
  }

  // Returns null when the header row is missing; rows without a title are skipped
  public static List<CsvTask>? Read(string csv)
  {
    var rows = ParseRows(csv.TrimStart('﻿'));
    if(rows.Count == 0 || !rows[0][0].Trim().Equals("Title", StringComparison.OrdinalIgnoreCase)) return null;

    var tasks = new List<CsvTask>();
    foreach(var row in rows.Skip(1))
    {
      var title = Unescape(Get(row, 0).Trim());
      if(title.Length == 0) continue;

      var description = Unescape(Get(row, 1).Trim());
      var priority = Enum.TryParse<Priority>(Get(row, 2).Trim(), true, out var p) ? p : Priority.Medium;
      DateTime? due = DateTime.TryParseExact(Get(row, 3).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
      var completed = Get(row, 4).Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
      tasks.Add(new CsvTask(title, description.Length == 0 ? null : description, priority, due, completed));
    }
    return tasks;
  }

  private static string Unescape(string value) =>
    value.Length > 1 && value[0] == '\'' && "=+-@".Contains(value[1]) ? value[1..] : value;

  private static string Get(List<string> row, int index) => index < row.Count ? row[index] : string.Empty;

  private static string Escape(string value)
  {
    // A leading =,+,-,@ would be run as a formula when the file is opened in Excel
    if(value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
    return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
  }

  private static List<List<string>> ParseRows(string text)
  {
    var rows = new List<List<string>>();
    var row = new List<string>();
    var field = new StringBuilder();
    var quoted = false;

    for(var i = 0; i < text.Length; i++)
    {
      var c = text[i];
      if(quoted)
      {
        if(c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
        else if(c == '"') quoted = false;
        else field.Append(c);
      }
      else if(c == '"') quoted = true;
      else if(c == ',') { row.Add(field.ToString()); field.Clear(); }
      else if(c == '\r' || c == '\n')
      {
        if(c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
        row.Add(field.ToString()); field.Clear();
        if(row.Any(f => f.Length > 0)) rows.Add(row);
        row = [];
      }
      else field.Append(c);
    }

    row.Add(field.ToString());
    if(row.Any(f => f.Length > 0)) rows.Add(row);
    return rows;
  }
}
