using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Tauchplaetze.Web.Components.Design;

/// <summary>Ein SVG-Element <c>&lt;text&gt;</c>. Razor deutet <c>&lt;text&gt;</c> in Code-Blöcken als eigenes Tag. Darum entsteht das Element hier.</summary>
public class SvgText : ComponentBase
{
    [Parameter]
    public string? Content { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? Attributes { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "text");
        builder.AddMultipleAttributes(1, Attributes);
        builder.AddContent(2, Content);
        builder.CloseElement();
    }
}
