namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Die Ausbaustufe eines Platzes (`cms.md`, «Ausbaustufen»). Die Regel steht nur hier.</summary>
internal static class ContentLevelRule
{
    public static byte For(DiveSiteContent content, Guid? heroMediaId)
    {
        var german = content.TextIn("de");
        var basic = content.MaxDepth is not null
                    && content.DifficultyRating is not null
                    && content.Access is not null
                    && content.EntryPointList.Any(e => e.Texts.Any(t => t.Culture == "de" && !string.IsNullOrWhiteSpace(t.Description)))
                    && !string.IsNullOrWhiteSpace(german?.ApproachInfo);

        if (!basic)
        {
            return 0;
        }

        return !string.IsNullOrWhiteSpace(german?.UnderwaterDescription) && heroMediaId is not null ? (byte)2 : (byte)1;
    }
}
