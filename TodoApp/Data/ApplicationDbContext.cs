using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain;

namespace TodoApp.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{

  public DbSet<TodoList> TodoLists { get; set; }
  public DbSet<TodoItem> TodoItems { get; set; }
  public DbSet<TodoListShare> TodoListShares { get; set; }

  protected override void OnModelCreating(ModelBuilder builder)
  {
    base.OnModelCreating(builder);

    builder.Entity<TodoList>()
          .HasQueryFilter(l=> !l.IsDeleted);

    builder.Entity<TodoListShare>()
          .HasKey(s => new {s.ListId, s.UserId});

    builder.Entity<TodoItem>()
          .HasOne(i => i.List)
          .WithMany(l => l.Items)
          .HasForeignKey(i => i.ListId);

    builder.Entity<TodoListShare>()
          .HasOne(s => s.List)
          .WithMany(l => l.Shares)
          .HasForeignKey(s => s.ListId);

  }
}
