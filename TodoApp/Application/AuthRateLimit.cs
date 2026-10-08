using System.Threading.RateLimiting;

namespace TodoApp.Application;

/// <summary>Limits form submissions that can be abused for password guessing, mass registration or email flooding.</summary>
public static class AuthRateLimit
{
  public const int PermitsPerMinute = 10;

  private static readonly string[] SensitivePaths =
    ["/Account/Login", "/Account/Register", "/Account/ForgotPassword", "/Account/ResendEmailConfirmation", "/Account/LoginWith2fa", "/Account/LoginWithRecoveryCode"];

  public static bool IsSensitive(string path, string method) =>
    HttpMethods.IsPost(method) && SensitivePaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase));

  public static RateLimitPartition<string> Partition(HttpContext http) =>
    IsSensitive(http.Request.Path, http.Request.Method)
      ? RateLimitPartition.GetFixedWindowLimiter(
          http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
          _ => new FixedWindowRateLimiterOptions { PermitLimit = PermitsPerMinute, Window = TimeSpan.FromMinutes(1) })
      : RateLimitPartition.GetNoLimiter("unlimited");
}
