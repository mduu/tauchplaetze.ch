# ADR-003: Azure SQL Database (Free Offer) und Blob Storage

## Status

**Akzeptiert** (17.10.2026)

## Kontext

- Der Betrieb soll unter CHF 20 pro Monat kosten.
- Die Daten sind relational: Seen, Plätze, Revisionen, Übersetzungen, Prognosen, Meldungen, Benutzer.
- Die Umkreissuche («Plätze im Umkreis von 60 km») ist zentral.
- Zur Wahl standen Azure SQL, Azure Table Storage und Cosmos DB.

## Entscheidung

Wir entscheiden uns für **Azure SQL Database mit dem Free Offer** für alle strukturierten Daten und **Azure Blob Storage** für Medien, Rohdaten und Modelle.

## Optionen

### Option A: Azure SQL Free Offer (gewählt)

**Vorteile:**
- 100'000 vCore-Sekunden und 32 GB pro Monat gratis, unbefristet.
- Relationales Modell, EF Core, Migrationen.
- `geography` mit NetTopologySuite für die Umkreissuche in der Datenbank.
- Marc kennt SQL Server sehr gut (feldnah läuft ebenfalls auf Azure SQL).

**Nachteile:**
- Auto-Pause verzögert die erste Anfrage nach einer Pause.
- Am Limit pausiert die Datenbank (oder kostet, je nach Einstellung).

### Option B: Cosmos DB

**Vorteile:**
- Gratis-Tarif vorhanden.

**Nachteile:**
- Joins, Umkreissuche und Revisionen sind umständlicher.
- RU-Modell braucht zusätzlich Caching. Kein Vorteil für dieses Projekt.

### Option C: Table Storage

**Vorteile:**
- Sehr günstig.

**Nachteile:**
- Keine Joins, keine Geoabfragen, keine Transaktionen über Partitionen. Für ein CMS zu eingeschränkt.

## Konsequenzen

### Positive Auswirkungen

- Datenbankkosten CHF 0.
- Einfache Abfragen und Migrationen.

### Negative Auswirkungen / Risiken

- Verbrauch muss überwacht werden (Alert bei unter 10 % Restkontingent).
- Output Cache ist Pflicht, damit die Datenbank wenig Last hat.
- Rohdaten gehören in Blob Storage, damit die 32 GB reichen.

## Referenzen

- `infrastructure.md`, Abschnitt «Azure SQL Free Offer»
- Microsoft Learn: Azure SQL Database free offer
