using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Extensions.Localization;
using TodoApp.Application;
using TodoApp.Resources;

namespace TodoApp.Tests;

public sealed class IdentityLocalizationTests
{
    private sealed class Model
    {
        [RequiredL] public string Email { get; set; } = "";
        [RequiredL, LengthL(10, 100)] public string Password { get; set; } = "";
        [CompareL("Password")] public string ConfirmPassword { get; set; } = "";
    }

    private sealed class EmailModel
    {
        [EmailL] public string Email { get; set; } = "";
    }

    private static List<string> Validate(object model, string culture)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
            return results.Select(r => r.ErrorMessage!).ToList();
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Theory]
    [InlineData("en", "The “Email” field is required.")]
    [InlineData("sr-Latn-RS", "Polje „Email“ je obavezno.")]
    public void Required_UsesTheCurrentCulture(string culture, string expected) =>
        Assert.Contains(expected, Validate(new Model(), culture));

    [Fact]
    public void Length_ReportsTheLimits_AndCompareReportsMismatch()
    {
        var errors = Validate(new Model { Email = "a@b.c", Password = "short", ConfirmPassword = "other" }, "en");

        Assert.Contains("The “Password” field must be between 10 and 100 characters long.", errors);
        Assert.Contains("The password and confirmation password do not match.", errors);
    }

    [Fact]
    public void Email_AcceptsValidAndRejectsInvalidAddresses()
    {
        Assert.Empty(Validate(new EmailModel { Email = "name@example.com" }, "en"));
        Assert.Contains("The “Email” field is not a valid email address.", Validate(new EmailModel { Email = "nope" }, "en"));
    }

    [Fact]
    public void IdentityErrors_AreLocalized()
    {
        var describer = new LocalizedIdentityErrorDescriber(new ResourceLocalizer());

        Assert.Equal("Lozinka mora imati najmanje 10 karaktera.", describer.PasswordTooShort(10).Description);
        Assert.Equal("Nalog sa ovom email adresom već postoji.", describer.DuplicateEmail("a@b.c").Description);
    }

    // Reads the real .resx files, like the app does at runtime
    private sealed class ResourceLocalizer : IStringLocalizer<SharedResource>
    {
        private static readonly System.Resources.ResourceManager Resources = new(typeof(SharedResource));

        public LocalizedString this[string name] => new(name, Resources.GetString(name, new CultureInfo("sr-Latn-RS")) ?? name);
        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(this[name].Value, arguments));
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
