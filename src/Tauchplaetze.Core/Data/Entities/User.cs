using Microsoft.AspNetCore.Identity;

namespace Tauchplaetze.Core.Data.Entities;

internal class User : IdentityUser
{
    [PersonalData]
    public required string DisplayName { get; set; }

    [PersonalData]
    public bool ShowNamePublicly { get; set; }

    [PersonalData]
    public string PreferredCulture { get; set; } = "de";

    [PersonalData]
    public DateTimeOffset? BlockedAt { get; set; }

    public string? BlockedBy { get; set; }
}
