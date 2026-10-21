# ADR-004: Prognose zuerst regelbasiert, ML-Modell später

## Status

**Akzeptiert** (17.10.2026)

## Kontext

- Ein ML-Modell braucht Labels (beobachtete Sichtweiten). Zum Start gibt es keine.
- Community-Meldungen bleiben in der Schweiz erfahrungsgemäss spärlich, auch auf bestehenden Plattformen.
- Die Prognose muss vom ersten Tag an für alle Plätze funktionieren.
- Erfahrene Taucher sollen die Bewertung nachvollziehen können.

## Entscheidung

Wir entscheiden uns für **einen regelbasierten Tauch-Index (`rules-1.0`)** im MVP und Phase 1. Ein **ML-Modell (ML.NET, LightGBM)** folgt in Phase 2, sobald genug Labels vorliegen. Es ersetzt die Regeln nur, wenn es im Backtesting klar besser ist.

## Optionen

### Option A: Regeln (gewählt für den Start)

**Vorteile:**
- Funktioniert ohne Trainingsdaten.
- Erklärbar («schlechter wegen Regen in den Zuflüssen»).
- Gewichte konfigurierbar ohne neuen Code.

**Nachteile:**
- Grobe Annahmen. Genauigkeit begrenzt.

### Option B: Sofort ein ML-Modell

**Nachteile:**
- Ohne Labels nicht trainierbar.

### Option C: Nur Rohdaten anzeigen, kein Index

**Nachteile:**
- Beantwortet die Kernfrage «Wo soll ich tauchen?» nicht.

## Konsequenzen

### Positive Auswirkungen

- Sofort nutzbar, transparent.
- Rohdaten werden ab Tag 1 gespeichert. Sie sind später die Features für das Modell.

### Negative Auswirkungen / Risiken

- Der Index kann anfangs danebenliegen. Die Oberfläche kennzeichnet ihn als Indikator.

## Referenzen

- `forecast.md`
