using System.Collections;
using System.Globalization;
using System.Resources;
using TodoApp.Resources;

namespace TodoApp.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public void EverySerbianTextHasAnEnglishTranslation()
    {
        var resources = new ResourceManager(typeof(SharedResource));

        var serbian = Keys(resources.GetResourceSet(CultureInfo.InvariantCulture, true, false));
        var english = Keys(resources.GetResourceSet(new CultureInfo("en"), true, false));

        Assert.NotEmpty(serbian);
        Assert.Equal(serbian, english);
    }

    private static SortedSet<string> Keys(ResourceSet? set) =>
        new(set?.Cast<DictionaryEntry>().Select(e => (string)e.Key) ?? []);
}
