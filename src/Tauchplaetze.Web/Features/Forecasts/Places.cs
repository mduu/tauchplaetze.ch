namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Orte für den Umkreis der Rangliste. Der Name kommt aus `Place_{Key}` in den Ressourcen.</summary>
internal record Place(string Key, double Latitude, double Longitude);

internal static class Places
{
    public const int DefaultRadiusKm = 60;

    public static readonly IReadOnlyList<Place> All =
    [
        new("zuerich", 47.3769, 8.5417),
        new("bern", 46.9480, 7.4474),
        new("basel", 47.5596, 7.5886),
        new("luzern", 47.0502, 8.3093),
        new("stgallen", 47.4245, 9.3767),
        new("lausanne", 46.5197, 6.6323),
        new("geneve", 46.2044, 6.1432),
        new("lugano", 46.0037, 8.9511),
        new("chur", 46.8508, 9.5320),
    ];

    public static Place? Find(string? key) => All.FirstOrDefault(p => p.Key == key);
}
