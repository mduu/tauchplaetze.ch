namespace Tauchplaetze.Core.Data.Entities;

internal class DiveSiteTranslation
{
    public Guid DiveSiteId { get; set; }

    public required string Culture { get; set; }

    public string? UnderwaterDescription { get; set; }

    public string? SurroundingsInfo { get; set; }

    public string? ApproachInfo { get; set; }

    public string? HazardsInfo { get; set; }

    public string? FacilitiesInfo { get; set; }

    public string? DivingBanNote { get; set; }

    public bool IsMachineTranslated { get; set; }
}
