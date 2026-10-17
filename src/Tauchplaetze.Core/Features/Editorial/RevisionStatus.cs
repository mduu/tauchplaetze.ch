namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Status einer Version eines Tauchplatzes (`cms.md`, «Versionen»).</summary>
public enum RevisionStatus
{
    /// <summary>Änderungsvorschlag, wartet auf Freigabe.</summary>
    Pending,

    /// <summary>Freigegeben. Die neueste freigegebene Version ist online.</summary>
    Approved,

    /// <summary>Abgelehnt, mit Begründung.</summary>
    Rejected,

    /// <summary>Von der Autorin oder vom Autor zurückgezogen.</summary>
    Withdrawn,
}
