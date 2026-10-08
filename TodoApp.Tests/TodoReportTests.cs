using System.Text;
using Microsoft.Extensions.Localization;
using TodoApp.Application;
using TodoApp.Domain;
using TodoApp.Resources;

namespace TodoApp.Tests;

public sealed class TodoReportTests
{
    [Fact]
    public void Generate_ReturnsPdf_ForListWithSerbianCharacters()
    {
        var list = new TodoList { Name = "Šuma, čaj i đurđevak" };
        var items = new List<TodoItem>
        {
            new() { Title = "Žuti zadatak", Description = "Opis ćirilica? ne", Priority = Priority.High, DueDate = new DateTime(2026, 10, 11) },
            new() { Title = "Gotov", IsCompleted = true }
        };

        var pdf = TodoReport.Generate(list, items, new Dictionary<Guid, int> { [items[0].Id] = 2 }, new KeyLocalizer());

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
