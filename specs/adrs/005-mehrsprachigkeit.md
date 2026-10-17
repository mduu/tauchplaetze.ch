# ADR-005: Mehrsprachigkeit DE, EN, FR, IT ab dem ersten Release

## Status

**Akzeptiert** (17.10.2026)

## Kontext

- Die Schweiz hat vier Landessprachen. Viele gute Tauchplätze liegen in der Romandie (Neuenburgersee, Genfersee) und im Tessin.
- Taucher aus dem Ausland suchen auf Englisch.
- Bei feldnah hat das nachträgliche Einführen von Französisch Aufwand verursacht (Story #069, ADR-015 dort).
- Claude Code kann Oberflächentexte in derselben Änderung übersetzen.

## Entscheidung

Wir entscheiden uns für **vier Sprachen ab dem ersten Release**: Deutsch (Standard und Rückfall), Englisch, Französisch, Italienisch.

- Oberflächentexte nur aus `.resx`. Claude Code pflegt alle vier Dateien in derselben Änderung.
- Redaktionelle Inhalte: Deutsch Pflicht, andere Sprachen optional mit Rückfall auf Deutsch und Hinweis.
- URLs mit Sprachpräfix und übersetzten Pfadsegmenten.

## Optionen

### Option A: Nur Deutsch, später erweitern

**Nachteile:**
- Teure Nachrüstung (Erfahrung feldnah).
- Romandie und Tessin bleiben aussen vor.

### Option B: Vier Sprachen ab Start (gewählt)

**Vorteile:**
- Keine Altlasten. Jede neue Fläche ist sofort vollständig.
- Mehr Suchtraffic über vier Sprachen.

**Nachteile:**
- Jede Oberflächenänderung braucht vier Einträge. Claude Code übernimmt das.
- Redaktionelle Inhalte sind anfangs oft nur deutsch.

## Konsequenzen

### Positive Auswirkungen

- Nationale Reichweite von Anfang an.

### Negative Auswirkungen / Risiken

- Übersetzungsqualität der Fachbegriffe: Glossar in `ubiquitous-language.md` ist verbindlich.
- Längere Texte in FR/IT: jede Fläche in allen Sprachen prüfen.

## Referenzen

- `i18n.md`, `ubiquitous-language.md`
- feldnah: ADR-015 Mehrsprachigkeit
