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

    /// <summary>Registrierung oder Anlage. null bei Konten von vor dem 19.10.2026.</summary>
    [PersonalData]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>Letzte erfolgreiche Anmeldung. Ohne IP-Adresse oder Gerät.</summary>
    [PersonalData]
    public DateTimeOffset? LastSignInAt { get; set; }
}
