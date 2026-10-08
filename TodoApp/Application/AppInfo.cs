namespace TodoApp.Application;

/// <summary>Branding, legal operator details and analytics settings, all read from configuration so they can change without a rebuild.</summary>
public sealed class AppInfo(IConfiguration config)
{
  public string Name => Value("App:Name") ?? "TodoApp";
  public string? PublicUrl => Value("App:PublicUrl")?.TrimEnd('/');

  public string? OperatorName => Value("Legal:OperatorName");
  public string? ContactEmail => Value("Legal:ContactEmail");
  public bool LegalConfigured => OperatorName is not null && ContactEmail is not null;

  public string? AnalyticsProvider => Value("Analytics:Provider");
  public string? AnalyticsSiteId => Value("Analytics:SiteId");
  public string? AnalyticsScriptUrl => Value("Analytics:ScriptUrl");

  /// <summary>The analytics script tag attributes, or null when analytics is not configured.</summary>
  public AnalyticsScript? Analytics
  {
    get
    {
      if(AnalyticsSiteId is null) return null;
      return AnalyticsProvider?.ToLowerInvariant() switch
      {
        "plausible" => new AnalyticsScript(AnalyticsScriptUrl ?? "https://plausible.io/js/script.js", "data-domain", AnalyticsSiteId, "Plausible"),
        "umami" => new AnalyticsScript(AnalyticsScriptUrl ?? "https://cloud.umami.is/script.js", "data-website-id", AnalyticsSiteId, "Umami"),
        _ => null
      };
    }
  }

  private string? Value(string key) => string.IsNullOrWhiteSpace(config[key]) ? null : config[key]!.Trim();
}

public record AnalyticsScript(string Src, string IdAttribute, string Id, string ProviderName);
