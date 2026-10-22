namespace Tauchplaetze.Core.Features.Translations;

/// <summary>Übersetzt die Felder eines Platzes. In Prod die Claude API, im Test ein Double.</summary>
internal interface ITextTranslator
{
    /// <summary>Kennzeichnet Modell und Prompt. Ändert sich der Wert, gilt der Cache nicht mehr.</summary>
    string Version { get; }

    /// <summary>Übersetzt die Felder. Die Antwort hat dieselben Schlüssel, sonst gilt sie als unbrauchbar.</summary>
    Task<TranslatorReply> Translate(string sourceCulture, string targetCulture, IReadOnlyDictionary<string, string> fields, CancellationToken ct);
}

/// <summary>Die übersetzten Felder und die verbrauchten Tokens. <c>null</c> als Felder, wenn die Antwort unbrauchbar ist.</summary>
internal record TranslatorReply(IReadOnlyDictionary<string, string>? Fields, int InputTokens, int OutputTokens);
