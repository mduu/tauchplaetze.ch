using Microsoft.Extensions.Localization;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Web.Localization;

namespace Tauchplaetze.Web.Features.Accounts;

internal static class AccountText
{
    public static IReadOnlyList<string> Errors(IStringLocalizer<AccountStrings> localizer, AccountResult result) =>
        result.Errors.Select(error => localizer[$"Error_{error}"].Value).ToList();
}
