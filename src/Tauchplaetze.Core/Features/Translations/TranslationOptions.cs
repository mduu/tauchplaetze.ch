namespace Tauchplaetze.Core.Features.Translations;

/// <summary>Maschinelle Übersetzung mit der Claude API (Abschnitt `Translation`, `cms.md`, «Automatisch übersetzen»).</summary>
public class TranslationOptions
{
    public const string Section = "Translation";

    /// <summary>Schlüssel der Claude API. Lokal als User Secret, in Prod im Key Vault. Ohne Wert ist die Funktion aus.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-haiku-5-5";

    /// <summary>Übersetzungen pro Schweizer Tag für Mitwirkende.</summary>
    public int DailyLimitContributor { get; set; } = 50;

    /// <summary>Übersetzungen pro Schweizer Tag für die Moderation. Admins haben kein Tageslimit.</summary>
    public int DailyLimitModerator { get; set; } = 500;

    /// <summary>Höchstens so viel kosten alle Übersetzungen eines Monats zusammen, in USD.</summary>
    public decimal MonthlyBudgetUsd { get; set; } = 5m;

    /// <summary>Preis pro Million Input-Tokens in USD.</summary>
    public decimal InputPricePerMillion { get; set; } = 0.10m;

    /// <summary>Preis pro Million Output-Tokens in USD.</summary>
    public decimal OutputPricePerMillion { get; set; } = 0.50m;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
