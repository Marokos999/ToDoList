using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TodoApp.Application;
using TodoApp.Data;

namespace TodoApp.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private ServiceProvider services = null!;

    public TodoNotifier Notifier => services.GetRequiredService<TodoNotifier>();

    public PostgresFixture()
    {
        // Same switch as Program.cs: the model and migrations rely on Npgsql's legacy timestamp mapping
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDbContextFactory<ApplicationDbContext>(options => options.UseNpgsql(container.GetConnectionString()));
        // Must match Program.cs, otherwise the model (passkeys table) differs from the migrations
        collection.AddIdentityCore<ApplicationUser>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
                  .AddEntityFrameworkStores<ApplicationDbContext>();
        collection.AddSingleton<TodoNotifier>();
        collection.AddScoped<ITodoService, TodoService>();
        services = collection.BuildServiceProvider();

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await services.DisposeAsync();
        await container.DisposeAsync();
    }

    public AsyncServiceScope CreateScope() => services.CreateAsyncScope();
}
