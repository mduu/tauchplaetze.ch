using Microsoft.AspNetCore.Identity;

namespace Tauchplaetze.Core.Features.Accounts;

internal static class AccountErrors
{
    public static AccountResult From(IdentityResult result) => result.Succeeded
        ? AccountResult.Ok
        : new AccountResult(result.Errors.Select(e => e.Code switch
        {
            "DuplicateEmail" or "DuplicateUserName" => AccountError.EmailTaken,
            "InvalidEmail" or "InvalidUserName" => AccountError.InvalidEmail,
            "PasswordTooShort" => AccountError.PasswordTooShort,
            "InvalidToken" => AccountError.InvalidToken,
            _ => AccountError.Other,
        }).Distinct().ToList());

    public static AccountError? DisplayName(string displayName) =>
        string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 60 ? AccountError.InvalidDisplayName : null;
}
