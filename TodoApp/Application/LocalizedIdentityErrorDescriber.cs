using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using TodoApp.Resources;

namespace TodoApp.Application;

public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> L) : IdentityErrorDescriber
{
  public override IdentityError DefaultError() => Error(nameof(DefaultError), "IdErrDefault");
  public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "IdErrPasswordMismatch");
  public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "IdErrInvalidToken");
  public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), "IdErrInvalidEmail");
  public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "IdErrInvalidEmail");
  public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), "IdErrDuplicateEmail");
  public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "IdErrDuplicateEmail");
  public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), "IdErrPasswordTooShort", length);
  public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "IdErrPasswordNonAlphanumeric");
  public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "IdErrPasswordDigit");
  public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "IdErrPasswordLower");
  public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "IdErrPasswordUpper");
  public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), "IdErrPasswordUnique", uniqueChars);

  private IdentityError Error(string code, string key, params object[] args) =>
    new() { Code = code, Description = L[key, args].Value };
}
