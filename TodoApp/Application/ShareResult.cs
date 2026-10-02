namespace TodoApp.Application;

public enum ShareResult
{
    Shared,
    NotOwner,
    UserNotFound,
    SelfShare,
    AlreadyShared
}
