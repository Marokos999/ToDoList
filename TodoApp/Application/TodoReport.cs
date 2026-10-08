using Microsoft.Extensions.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TodoApp.Domain;
using TodoApp.Resources;

namespace TodoApp.Application;

public static class TodoReport
{
  static TodoReport() => QuestPDF.Settings.License = LicenseType.Community;

  public static byte[] Generate(TodoList list, IReadOnlyList<TodoItem> items,
                                IReadOnlyDictionary<Guid, int> attachmentCounts, IStringLocalizer<SharedResource> L)
  {
    var done = items.Count(i => i.IsCompleted);
    var dateFormat = L["DateFormat"].Value;

    return Document.Create(doc => doc.Page(page =>
    {
      page.Size(PageSizes.A4);
      page.Margin(40);
      page.DefaultTextStyle(t => t.FontSize(10));

      page.Header().Column(col =>
      {
        col.Item().Text(list.Name).FontSize(20).Bold();
        col.Item().Text(L["ReportSummary", items.Count, done, items.Count - done].Value).FontColor(Colors.Grey.Darken1);
        col.Item().PaddingBottom(10).Text(L["ReportGenerated", DateTime.Now.ToString(dateFormat)].Value).FontColor(Colors.Grey.Darken1);
      });

      page.Content().Table(table =>
      {
        table.ColumnsDefinition(c =>
        {
          c.RelativeColumn(5);
          c.RelativeColumn(1.3f);
          c.RelativeColumn(1.5f);
          c.RelativeColumn(1.5f);
        });

        table.Header(h =>
        {
          foreach (var title in new[] { "Title", "Priority", "DueDate", "Status" })
            h.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(L[title].Value).Bold();
        });

        foreach (var item in items)
        {
          var cell = (IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);
          var attachments = attachmentCounts.GetValueOrDefault(item.Id);

          table.Cell().Element(cell).Column(col =>
          {
            var title = col.Item().Text(item.Title);
            if (item.IsCompleted) title.Strikethrough().FontColor(Colors.Grey.Darken1);
            if (!string.IsNullOrWhiteSpace(item.Description))
              col.Item().Text(item.Description).FontSize(9).FontColor(Colors.Grey.Darken1);
            if (attachments > 0)
              col.Item().Text(L["ReportAttachments", attachments].Value).FontSize(9).FontColor(Colors.Grey.Darken1);
          });
          table.Cell().Element(cell).Text(PriorityName(item.Priority, L));
          table.Cell().Element(cell).Text(item.DueDate?.ToString(dateFormat) ?? "-");
          table.Cell().Element(cell).Text(item.IsCompleted ? L["FilterCompleted"].Value : L["FilterActive"].Value);
        }
      });

      page.Footer().AlignCenter().Text(t =>
      {
        t.CurrentPageNumber();
        t.Span(" / ");
        t.TotalPages();
      });
    })).GeneratePdf();
  }

  private static string PriorityName(Priority p, IStringLocalizer<SharedResource> L) => p switch
  {
    Priority.High => L["PriorityHigh"],
    Priority.Medium => L["PriorityMedium"],
    _ => L["PriorityLow"]
  };
}
