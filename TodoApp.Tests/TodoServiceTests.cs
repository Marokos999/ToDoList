using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TodoApp.Application;
using TodoApp.Data;
using TodoApp.Domain;

namespace TodoApp.Tests;

public sealed class TodoServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private AsyncServiceScope scope;
    private ITodoService service = null!;
    private UserManager<ApplicationUser> users = null!;

    public Task InitializeAsync()
    {
        scope = fixture.CreateScope();
        service = scope.ServiceProvider.GetRequiredService<ITodoService>();
        users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => scope.DisposeAsync().AsTask();

    [Fact]
    public async Task Stranger_CannotSeeList()
    {
        var owner = await CreateUserAsync();
        var stranger = await CreateUserAsync();
        var list = await service.CreateListAsync("Private", owner.Id);
        await service.AddItemAsync(list.Id, "Task", Priority.Medium, null, owner.Id);

        Assert.Null(await service.GetListAsync(list.Id, stranger.Id));
        Assert.Empty(await service.GetItemsAsync(list.Id, stranger.Id));
    }

    [Fact]
    public async Task Stranger_CannotAddToggleEditOrDeleteItems()
    {
        var owner = await CreateUserAsync();
        var stranger = await CreateUserAsync();
        var list = await service.CreateListAsync("Private", owner.Id);
        var item = await service.AddItemAsync(list.Id, "Task", Priority.Medium, null, owner.Id);

        Assert.Null(await service.AddItemAsync(list.Id, "Intruder", Priority.High, null, stranger.Id));
        await service.ToggleCompleteAsync(item!.Id, stranger.Id);
        await service.UpdateItemAsync(item.Id, "Hacked", Priority.High, null, "x", stranger.Id);
        await service.DeleteItemAsync(item.Id, stranger.Id);

        var unchanged = Assert.Single(await service.GetItemsAsync(list.Id, owner.Id));
        Assert.Equal("Task", unchanged.Title);
        Assert.False(unchanged.IsCompleted);
    }

    [Fact]
    public async Task SharedUser_CanSeeListAndAddItems()
    {
        var owner = await CreateUserAsync();
        var friend = await CreateUserAsync();
        var list = await service.CreateListAsync("Shared", owner.Id);
        await service.ShareListAsync(list.Id, owner.Id, friend.Email!);

        Assert.NotNull(await service.GetListAsync(list.Id, friend.Id));
        Assert.NotNull(await service.AddItemAsync(list.Id, "From friend", Priority.Medium, null, friend.Id));
        Assert.Single(await service.GetItemsAsync(list.Id, owner.Id));
    }

    [Fact]
    public async Task ShareListAsync_SharesOnce_AndIgnoresEmailCaseAndSpaces()
    {
        var owner = await CreateUserAsync();
        var friend = await CreateUserAsync();
        var list = await service.CreateListAsync("Shared", owner.Id);

        Assert.Equal(ShareResult.Shared, await service.ShareListAsync(list.Id, owner.Id, $"  {friend.Email!.ToUpperInvariant()} "));
        Assert.Equal(ShareResult.AlreadyShared, await service.ShareListAsync(list.Id, owner.Id, friend.Email!));
    }

    [Fact]
    public async Task ShareListAsync_ReturnsUserNotFound_ForUnknownEmail()
    {
        var owner = await CreateUserAsync();
        var list = await service.CreateListAsync("List", owner.Id);

        Assert.Equal(ShareResult.UserNotFound, await service.ShareListAsync(list.Id, owner.Id, "nobody@test.local"));
    }

    [Fact]
    public async Task ShareListAsync_ReturnsSelfShare_ForOwnEmail()
    {
        var owner = await CreateUserAsync();
        var list = await service.CreateListAsync("List", owner.Id);

        Assert.Equal(ShareResult.SelfShare, await service.ShareListAsync(list.Id, owner.Id, owner.Email!));
    }

    [Fact]
    public async Task ShareListAsync_ReturnsNotOwner_WhenSharedUserShares()
    {
        var owner = await CreateUserAsync();
        var friend = await CreateUserAsync();
        var third = await CreateUserAsync();
        var list = await service.CreateListAsync("Shared", owner.Id);
        await service.ShareListAsync(list.Id, owner.Id, friend.Email!);

        Assert.Equal(ShareResult.NotOwner, await service.ShareListAsync(list.Id, friend.Id, third.Email!));
    }

    [Fact]
    public async Task ToggleCompleteAsync_SetsAndClearsCompletedAt()
    {
        var owner = await CreateUserAsync();
        var list = await service.CreateListAsync("List", owner.Id);
        var item = await service.AddItemAsync(list.Id, "Task", Priority.Low, null, owner.Id);

        await service.ToggleCompleteAsync(item!.Id, owner.Id);
        var done = Assert.Single(await service.GetItemsAsync(list.Id, owner.Id));
        Assert.True(done.IsCompleted);
        Assert.NotNull(done.CompletedAt);

        await service.ToggleCompleteAsync(item.Id, owner.Id);
        var undone = Assert.Single(await service.GetItemsAsync(list.Id, owner.Id));
        Assert.False(undone.IsCompleted);
        Assert.Null(undone.CompletedAt);
    }

    [Fact]
    public async Task UpdateItemAsync_UpdatesAllFields()
    {
        var owner = await CreateUserAsync();
        var list = await service.CreateListAsync("List", owner.Id);
        var item = await service.AddItemAsync(list.Id, "Old", Priority.Low, null, owner.Id);
        var due = new DateTime(2026, 10, 3);

        await service.UpdateItemAsync(item!.Id, "  New  ", Priority.High, due, "   ", owner.Id);

        var updated = Assert.Single(await service.GetItemsAsync(list.Id, owner.Id));
        Assert.Equal("New", updated.Title);
        Assert.Equal(Priority.High, updated.Priority);
        Assert.Equal(due, updated.DueDate);
        Assert.Null(updated.Description);
    }

    [Fact]
    public async Task MoveItemAsync_MovesItemToTargetPosition_AndStrangerCannotMove()
    {
        var owner = await CreateUserAsync();
        var stranger = await CreateUserAsync();
        var list = await service.CreateListAsync("List", owner.Id);
        var a = await service.AddItemAsync(list.Id, "A", Priority.Low, null, owner.Id);
        var b = await service.AddItemAsync(list.Id, "B", Priority.Low, null, owner.Id);
        var c = await service.AddItemAsync(list.Id, "C", Priority.Low, null, owner.Id);

        await service.MoveItemAsync(c!.Id, a!.Id, stranger.Id);
        Assert.Equal(["A", "B", "C"], (await service.GetItemsAsync(list.Id, owner.Id)).Select(i => i.Title));

        await service.MoveItemAsync(c.Id, a.Id, owner.Id);
        Assert.Equal(["C", "A", "B"], (await service.GetItemsAsync(list.Id, owner.Id)).Select(i => i.Title));

        await service.MoveItemAsync(c.Id, b!.Id, owner.Id);
        Assert.Equal(["A", "B", "C"], (await service.GetItemsAsync(list.Id, owner.Id)).Select(i => i.Title));
    }

    [Fact]
    public async Task Attachments_CanBeAddedDownloadedAndDeleted_OnlyByUsersWithAccess()
    {
        var owner = await CreateUserAsync();
        var stranger = await CreateUserAsync();
        var list = await service.CreateListAsync("List", owner.Id);
        var item = await service.AddItemAsync(list.Id, "Task", Priority.Low, null, owner.Id);
        var bytes = "hello"u8.ToArray();

        Assert.False(await service.AddAttachmentAsync(item!.Id, "a.txt", "text/plain", new MemoryStream(bytes), stranger.Id));
        Assert.True(await service.AddAttachmentAsync(item.Id, "../a.txt", "text/plain", new MemoryStream(bytes), owner.Id));
        Assert.False(await service.AddAttachmentAsync(item.Id, "big.bin", "", new MemoryStream(new byte[ITodoService.MaxAttachmentBytes + 1]), owner.Id));

        var info = Assert.Single(await service.GetAttachmentsAsync(item.Id, owner.Id));
        Assert.Equal("a.txt", info.FileName);
        Assert.Empty(await service.GetAttachmentsAsync(item.Id, stranger.Id));
        Assert.Null(await service.GetAttachmentAsync(info.Id, stranger.Id));
        Assert.Equal(bytes, (await service.GetAttachmentAsync(info.Id, owner.Id))!.Data);

        await service.DeleteAttachmentAsync(info.Id, stranger.Id);
        Assert.Single(await service.GetAttachmentsAsync(item.Id, owner.Id));
        await service.DeleteAttachmentAsync(info.Id, owner.Id);
        Assert.Empty(await service.GetAttachmentsAsync(item.Id, owner.Id));
    }

    [Fact]
    public async Task CsvExportAndImport_RoundTripsTasks_AndRejectsInvalidFiles()
    {
        var owner = await CreateUserAsync();
        var stranger = await CreateUserAsync();
        var source = await service.CreateListAsync("Source", owner.Id);
        var item = await service.AddItemAsync(source.Id, "Say \"hi\", =now", Priority.High, new DateTime(2026, 10, 11), owner.Id);
        await service.UpdateItemAsync(item!.Id, item.Title, Priority.High, item.DueDate, "line1\nline2", owner.Id);
        await service.ToggleCompleteAsync(item.Id, owner.Id);

        var csv = await service.ExportCsvAsync(source.Id, owner.Id);
        Assert.Null(await service.ExportCsvAsync(source.Id, stranger.Id));

        var target = await service.CreateListAsync("Target", owner.Id);
        Assert.Null(await service.ImportCsvAsync(target.Id, csv!, stranger.Id));
        Assert.Null(await service.ImportCsvAsync(target.Id, "not,a,csv\n1,2,3", owner.Id));
        Assert.Equal(1, await service.ImportCsvAsync(target.Id, csv!, owner.Id));

        var imported = Assert.Single(await service.GetItemsAsync(target.Id, owner.Id));
        Assert.Equal("Say \"hi\", =now", imported.Title);
        Assert.Equal("line1\nline2", imported.Description);
        Assert.Equal(Priority.High, imported.Priority);
        Assert.Equal(new DateTime(2026, 10, 11), imported.DueDate);
        Assert.True(imported.IsCompleted);
    }

    [Fact]
    public async Task DeleteUserDataAsync_RemovesOwnedListsAndShares_ButKeepsOtherUsersData()
    {
        var leaving = await CreateUserAsync();
        var other = await CreateUserAsync();
        var owned = await service.CreateListAsync("Owned", leaving.Id);
        var item = await service.AddItemAsync(owned.Id, "Task", Priority.Low, null, leaving.Id);
        await service.AddAttachmentAsync(item!.Id, "a.txt", "text/plain", new MemoryStream([1]), leaving.Id);
        await service.ShareListAsync(owned.Id, leaving.Id, other.Email!);
        var deletedList = await service.CreateListAsync("Deleted", leaving.Id);
        await service.DeleteListAsync(deletedList.Id, leaving.Id);

        var othersList = await service.CreateListAsync("Others", other.Id);
        await service.ShareListAsync(othersList.Id, other.Id, leaving.Email!);

        await service.DeleteUserDataAsync(leaving.Id);

        Assert.Null(await service.GetListAsync(owned.Id, other.Id));
        Assert.Empty(await service.GetItemsAsync(owned.Id, other.Id));
        Assert.NotNull(await service.GetListAsync(othersList.Id, other.Id));
        Assert.Empty(await service.GetListsForUserAsync(leaving.Id));
        // The share granting the departing user access to someone else's list is gone too
        Assert.Null(await service.GetListAsync(othersList.Id, leaving.Id));
    }

    [Fact]
    public async Task ItemAndListChanges_NotifySubscribers()
    {
        var owner = await CreateUserAsync();
        var list = await service.CreateListAsync("List", owner.Id);
        var notifications = 0;
        Task OnChanged(Guid listId)
        {
            if (listId == list.Id) notifications++;
            return Task.CompletedTask;
        }

        fixture.Notifier.ListChanged += OnChanged;
        try
        {
            var item = await service.AddItemAsync(list.Id, "Task", Priority.Medium, null, owner.Id);
            await service.ToggleCompleteAsync(item!.Id, owner.Id);
            await service.UpdateItemAsync(item.Id, "Task", Priority.High, null, null, owner.Id);
            await service.DeleteItemAsync(item.Id, owner.Id);
            await service.RenameListAsync(list.Id, "Renamed", owner.Id);
            await service.DeleteListAsync(list.Id, owner.Id);
        }
        finally
        {
            fixture.Notifier.ListChanged -= OnChanged;
        }

        Assert.Equal(6, notifications);
    }

    [Fact]
    public async Task GetListsForUserAsync_ReturnsOwnedAndSharedLists()
    {
        var owner = await CreateUserAsync();
        var friend = await CreateUserAsync();
        var own = await service.CreateListAsync("Own", friend.Id);
        var shared = await service.CreateListAsync("Shared", owner.Id);
        await service.CreateListAsync("Not shared", owner.Id);
        await service.ShareListAsync(shared.Id, owner.Id, friend.Email!);

        var lists = await service.GetListsForUserAsync(friend.Id);

        Assert.Equal(new[] { own.Id, shared.Id }.Order(), lists.Select(l => l.Id).Order());
    }

    [Fact]
    public async Task GetListsForUserAsync_ReturnsProgressAndSharedFlag()
    {
        var owner = await CreateUserAsync();
        var friend = await CreateUserAsync();
        var list = await service.CreateListAsync("Shared", owner.Id);
        var done = await service.AddItemAsync(list.Id, "Done", Priority.Low, null, owner.Id);
        await service.AddItemAsync(list.Id, "Open", Priority.Low, null, owner.Id);
        await service.ToggleCompleteAsync(done!.Id, owner.Id);
        await service.ShareListAsync(list.Id, owner.Id, friend.Email!);

        var forOwner = Assert.Single(await service.GetListsForUserAsync(owner.Id));
        var forFriend = Assert.Single(await service.GetListsForUserAsync(friend.Id));

        Assert.False(forOwner.IsShared);
        Assert.True(forFriend.IsShared);
        Assert.Equal((1, 2), (forFriend.CompletedItems, forFriend.TotalItems));
    }

    [Fact]
    public async Task RenameListAsync_OnlyOwnerCanRename()
    {
        var owner = await CreateUserAsync();
        var friend = await CreateUserAsync();
        var list = await service.CreateListAsync("Old", owner.Id);
        await service.ShareListAsync(list.Id, owner.Id, friend.Email!);

        await service.RenameListAsync(list.Id, "By friend", friend.Id);
        await service.RenameListAsync(list.Id, "  New  ", owner.Id);

        Assert.Equal("New", (await service.GetListAsync(list.Id, owner.Id))!.Name);
    }

    [Fact]
    public async Task DeleteListAsync_OnlyOwnerCanDelete_AndListDisappearsForEveryone()
    {
        var owner = await CreateUserAsync();
        var friend = await CreateUserAsync();
        var list = await service.CreateListAsync("To delete", owner.Id);
        await service.ShareListAsync(list.Id, owner.Id, friend.Email!);

        await service.DeleteListAsync(list.Id, friend.Id);
        Assert.NotNull(await service.GetListAsync(list.Id, owner.Id));

        await service.DeleteListAsync(list.Id, owner.Id);
        Assert.Null(await service.GetListAsync(list.Id, owner.Id));
        Assert.DoesNotContain(await service.GetListsForUserAsync(friend.Id), l => l.Id == list.Id);
    }

    private async Task<ApplicationUser> CreateUserAsync()
    {
        var email = $"{Guid.NewGuid():N}@test.local";
        var user = new ApplicationUser { UserName = email, Email = email };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        return user;
    }
}
