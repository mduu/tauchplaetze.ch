using Microsoft.Extensions.Localization;
using Tauchplaetze.Web.Localization;

namespace Tauchplaetze.Web.Features.Editorial;

/// <summary>Texte für die Feld-Codes des Vergleichs (`Text.de.ApproachInfo`, `EntryPoint.1.fr.Name`, `Notice.1.ValidUntil`, `Sketch.2.Depth`).</summary>
internal static class FieldLabels
{
    public static string For(IStringLocalizer<AdminStrings> l, string field)
    {
        var parts = field.Split('.');
        return parts switch
        {
            ["Text", var culture, var name] => $"{l[$"Field_{name}"]} ({culture.ToUpperInvariant()})",
            ["EntryPoint", var index, "Location"] => $"{l["Field_EntryPoint", index]} · {l["Field_Location"]}",
            ["EntryPoint", var index, "Kind"] => $"{l["Field_EntryPoint", index]} · {l["Field_EntryKind"]}",
            ["EntryPoint", var index, var culture, var name] => $"{l["Field_EntryPoint", index]} · {l[$"Field_Entry{name}"]} ({culture.ToUpperInvariant()})",
            ["Notice", var index, var name] => $"{l["Field_Notice", index]} · {l[$"Field_Notice{name}"]}",
            ["Notice", var index, var culture, "Text"] => $"{l["Field_Notice", index]} · {l["Field_NoticeText"]} ({culture.ToUpperInvariant()})",
            ["Sketch", var index, var name] => $"{l["Field_Sketch", index]} · {l[$"Field_Sketch{name}"]}",
            ["Sketch", var index, var culture, "Label"] => $"{l["Field_Sketch", index]} · {l["Field_SketchLabel"]} ({culture.ToUpperInvariant()})",
            _ => l[$"Field_{field}"],
        };
    }
}
