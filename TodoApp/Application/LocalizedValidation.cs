using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Resources;
using TodoApp.Resources;

namespace TodoApp.Application;

// Data annotation messages are resolved when validation runs, so they follow the request culture.
// Attribute arguments cannot use IStringLocalizer, hence the ResourceManager.
internal static class ValidationText
{
  private static readonly ResourceManager Resources = new(typeof(SharedResource));

  public static string Get(string key, params object[] args) =>
    string.Format(CultureInfo.CurrentUICulture, Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key, args);

  public static string Field(string name) => Resources.GetString("Field" + name, CultureInfo.CurrentUICulture) ?? name;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class RequiredLAttribute : RequiredAttribute
{
  public override string FormatErrorMessage(string name) => ValidationText.Get("ValRequired", ValidationText.Field(name));
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class EmailLAttribute : DataTypeAttribute
{
  private static readonly EmailAddressAttribute Inner = new();

  public EmailLAttribute() : base(DataType.EmailAddress) { }

  public override bool IsValid(object? value) => Inner.IsValid(value);

  public override string FormatErrorMessage(string name) => ValidationText.Get("ValEmail", ValidationText.Field(name));
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class LengthLAttribute : StringLengthAttribute
{
  private readonly int minimum;
  private readonly int maximum;

  public LengthLAttribute(int minimum, int maximum) : base(maximum)
  {
    this.minimum = minimum;
    this.maximum = maximum;
    MinimumLength = minimum;
  }

  public override string FormatErrorMessage(string name) => ValidationText.Get("ValLength", ValidationText.Field(name), minimum, maximum);
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class CompareLAttribute(string otherProperty) : CompareAttribute(otherProperty)
{
  public override string FormatErrorMessage(string name) => ValidationText.Get("ValCompare");
}
