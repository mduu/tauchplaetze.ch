# ADR-011: Inhalt von Prod nach lokal

## Status

**Akzeptiert** (19.10.2026)

## Kontext

- Marc erfasst die Details der Tauchplätze in Prod. Für die Entwicklung braucht er dieselben Inhalte lokal (Entscheid 19.10.2026).
- Die umgekehrte Richtung (lokal nach Prod) braucht es nicht.
- Die Inhalte liegen verteilt: Tabellen in Azure SQL, Versionen als JSON in Blob Storage (ADR-008), Fotos als WebP in Blob Storage (ADR-009).
- Ein Backup der Datenbank enthält Konten, E-Mail-Adressen, offene Vorschläge und private Fotos. Diese Daten sollen Prod nicht verlassen (Datenschutz).

## Entscheidung

Wir entscheiden uns für ein **Inhaltspaket (ZIP)**, das Prod exportiert und lokal ein Import übernimmt.

- **Export** (Query `ExportContent`, ohne Seiteneffekte): Gewässer mit den Feldern des Seeds und den Stationen, öffentliche und archivierte Plätze mit dem Inhalt der neuesten freigegebenen Version, Slug-Redirects, öffentliche Fotos mit Metadaten und ihren WebP-Grössen. Format in `datastructure.md`, «Inhaltspaket».
- **Nicht im Paket:** Konten, E-Mail-Adressen, Ids von Autoren und Uploadern, Kommentare und Status der Versionen, offene und abgelehnte Vorschläge, Entwürfe, private Fotos und Originale, Mails, Kontaktnachrichten, Prognosen. Das Paket enthält also nur, was ohnehin öffentlich ist. Der Name eines Fotografen ist die öffentliche Namensnennung des Fotos.
- **Zugang zum Export:** Admins über `/platform/inhalt` (Download), oder der Abruf `GET /api/platform/content-export` mit dem Header `X-Content-Export-Key`. Der Schlüssel ist das Secret `Content:ExportKey`. Ohne Schlüssel gibt der Endpoint 404, mit falschem Schlüssel 401. Der Vergleich ist zeitkonstant. 10 Abrufe pro Stunde und Adresse.
- **Import** (Command `ImportContent`): nur Admins und nur mit `Content:ImportEnabled = true`. Aspire setzt den Schalter lokal, in Prod fehlt er.
  - Gewässer: fehlende anlegen, fehlende Stationen ergänzen. Vorhandene Felder bleiben.
  - Platz nach Id: fehlt er, legt der Import ihn mit freigegebener Version 1 an. Ist der Inhalt anders (Hash der Version), hängt er eine freigegebene Version an. Ist er gleich, passiert nichts. Autor ist der importierende Admin, Kommentar «Import aus Prod vom dd.MM.yyyy».
  - Status Veröffentlicht bzw. Archiviert übernimmt der Import. Slug-Redirects ergänzt er.
  - Fotos: fehlende öffentliche Fotos legt er mit derselben Id und den Dateien im Container `media` an. Vorhandene bleiben.
  - Der Import löscht nie. Ein zweiter Import desselben Pakets ändert nichts.
  - Ist der Slug eines neuen Platzes lokal schon belegt, überspringt der Import ihn und zählt ihn.
- **Abruf lokal:** «Aus Prod übernehmen» auf `/platform/inhalt` ruft `PullContentFromSource`. Der Core lädt das Paket per HTTPS von `Content:SourceBaseUri` mit `Content:ExportKey` (User Secrets) und importiert es. Alternativ lädt Marc das ZIP von Hand hoch.

## Optionen

### Option A: Backup der Datenbank (bacpac) und Kopie der Blobs

**Vorteile:**
- Vollständig, keine eigene Logik.

**Nachteile:**
- Enthält alle Personendaten. Lokal liegen dann Konten und E-Mail-Adressen.
- Überschreibt lokale Daten. Die Versionen in Blob Storage müssen separat mitkommen.

### Option B: Inhaltspaket mit Import (gewählt)

**Vorteile:**
- Nur öffentliche Inhalte, keine Personendaten.
- Idempotent und additiv. Lokale Plätze und Versionen bleiben.
- Läuft über die bestehenden Regeln (Freigabe, Ausbaustufe, Redirects).

**Nachteile:**
- Eigener Code für Format und Import.
- Lokale Ids der Versionen und Autoren weichen von Prod ab.

## Konsequenzen

### Positive Auswirkungen

- Marc erfasst in Prod und holt die Inhalte mit einem Klick in die Entwicklung.
- Kein Personendatum verlässt Prod.

### Negative Auswirkungen / Risiken

- Mit dem Schlüssel kann jeder das Paket laden. Es enthält nur öffentliche Inhalte, der Schlüssel schützt vor allem gegen Last. Bei Verdacht den Schlüssel in Prod wechseln.
- Ein Paket mit vielen Fotos ist gross. Der Export schreibt in eine temporäre Datei, der Import liest höchstens `Content:MaxPackageBytes` (Standard 2 GB).
- Ändert sich das Format der Versionen, braucht der Import die Formatversion im Manifest.

### Später

- Ein Job lokal, der das Paket z.B. täglich holt. Dafür reicht ein Worker, der `PullContentFromSource` mit einem Admin aufruft.

## Referenzen

- ADR-008 (Versionen), ADR-009 (Fotos)
- `cms.md`, «Inhalt (Admin)»; `infrastructure.md`, «Umgebungen»; `datastructure.md`, «Inhaltspaket»
