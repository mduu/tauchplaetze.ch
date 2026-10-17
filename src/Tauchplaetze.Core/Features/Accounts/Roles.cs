namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>Rollen im CMS (`cms.md`, «Rollen»).</summary>
public static class Roles
{
    /// <summary>Mitwirkende: schlagen Änderungen vor.</summary>
    public const string Contributor = "Contributor";

    /// <summary>Moderation: gibt Vorschläge frei.</summary>
    public const string Moderator = "Moderator";

    /// <summary>Admin: alles, dazu Rollen und Zuordnungen.</summary>
    public const string Admin = "Admin";

    /// <summary>Alle Rollen.</summary>
    public static readonly IReadOnlyList<string> All = [Contributor, Moderator, Admin];
}
