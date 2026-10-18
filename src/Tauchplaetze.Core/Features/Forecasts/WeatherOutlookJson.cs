using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Das Format der Stunden in <c>SiteWeatherOutlook.Hours</c>. Enums als Namen, damit alte Rows lesbar bleiben, wenn ein Symbol dazukommt.</summary>
internal static class WeatherOutlookJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
