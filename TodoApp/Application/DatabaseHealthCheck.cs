using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TodoApp.Data;

namespace TodoApp.Application;

public sealed class DatabaseHealthCheck(IDbContextFactory<ApplicationDbContext> dbFactory) : IHealthCheck
{
  public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
  {
    try
    {
      await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
      return await db.Database.CanConnectAsync(cancellationToken)
        ? HealthCheckResult.Healthy()
        : HealthCheckResult.Unhealthy("Cannot connect to the database");
    }
    catch(Exception ex)
    {
      return HealthCheckResult.Unhealthy("Database check failed", ex);
    }
  }
}
