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

- **Export** (Query `ExportContent`, ohne Seiteneffekte): Gewässer mit den Feldern des Seeds und den Stationen, veröffentlichte und archivierte Plätze mit dem Inhalt der neuesten freigegebenen Version, Slug-Redirects, öffentliche Fotos mit Metadaten und ihren WebP-Grössen. Format in `datastructure.md`, «Inhaltspaket».
- **Nicht im Paket:** Konten, E-Mail-Adressen, Ids von Autoren und Uploadern, Kommentare und Status der Versionen, offene und abgelehnte Vorschläge, Entwürfe, private Fotos und Originale, Mails, Kontaktnachrichten, Prognosen. Der Name eines Fotografen ist die öffentliche Namensnennung des Fotos.
- **Archivierte Plätze** sind nicht mehr öffentlich. Ihr Inhalt war aber veröffentlicht, freigegeben von der Moderation, und enthält keine Personendaten. Darum kommen sie mit, damit lokal dieselben Slugs, Redirects und Archive bestehen. Fotos archivierter Plätze bleiben im öffentlichen Container und kommen ebenfalls mit.
- **Streaming:** Der Export schreibt das ZIP direkt in die Antwort. Die ersten Bytes kommen sofort, so brechen Cloudflare (100 s bis zur Antwort) und die Plattform (240 s ohne Daten) ein grosses Paket nicht ab. Das ZIP schliesst Einträge synchron. Der Core puffert darum je einen Eintrag (höchstens eine Bildgrösse) und schreibt ihn asynchron weiter. Die Fotos bleiben im Paket: Ein Paket genügt, und lokal braucht es keinen Zugriff auf die Bild-URLs von Prod.
- **Zugang zum Export:** Admins über `/platform/inhalt` (Download), oder der Abruf `GET /api/platform/content-export` mit dem Header `X-Content-Export-Key`. Der Schlüssel ist das Secret `Content:ExportKey`. Ohne Schlüssel gibt der Endpoint 404, mit falschem Schlüssel 401. Der Vergleich ist zeitkonstant. 10 Abrufe pro Stunde und Client, gezählt wie beim Kontaktformular (`CF-Connecting-IP` nur mit `Contact:TrustCloudflareHeader`, IPv6 als /64). Abgewiesen gibt 429.
- **Import** (Command `ImportContent`): nur Admins und nur mit `Content:ImportEnabled = true`. Aspire setzt den Schalter lokal, in Prod fehlt er.
  - Gewässer: fehlende anlegen, fehlende Stationen ergänzen. Vorhandene Felder bleiben.
  - Platz nach Id: fehlt er, legt der Import ihn mit freigegebener Version 1 an. Ist der Inhalt anders (Hash der Version), hängt er eine freigegebene Version an. Ist er gleich, passiert nichts. Autor ist der importierende Admin, Kommentar «Import aus Prod vom dd.MM.yyyy».
  - Status Veröffentlicht bzw. Archiviert übernimmt der Import. Slug-Redirects ergänzt er.
  - Fotos: fehlende öffentliche Fotos legt er mit derselben Id und den Dateien im Container `media` an. Vorhandene bleiben.
  - Der Import löscht nie. Ein zweiter Import desselben Pakets ändert nichts.
  - Belegt lokal ein anderer Platz oder ein Redirect eines anderen Platzes den Slug, überspringt der Import den Platz, neu oder vorhanden, und zählt ihn.
  - Jeder Platz durchläuft die Prüfung der Redaktion (`ContentValidation`: Slug, reservierte Slugs, Schweiz, Bewertungen, Namen). Ungültige oder unlesbare Plätze überspringt der Import und zählt sie.
  - Fehlt eine Bildgrösse eines Fotos oder ist sie zu gross, legt der Import das Foto nicht an, zählt es und schreibt eine Warnung ins Log.
  - Schutz gegen ZIP-Bomben: höchstens 50 MB je JSON-Datei und 20 MB je Bildgrösse (entpackt), das ganze Paket höchstens `Content:MaxPackageBytes`.
- **Abruf lokal:** «Aus Prod übernehmen» auf `/platform/inhalt` ruft `PullContentFromSource`. Der Core lädt das Paket von `Content:SourceBaseUri` mit `Content:ExportKey` (User Secrets) und importiert es. Nur HTTPS, ohne Weiterleitungen, der Header mit dem Schlüssel erscheint nicht im Log. Alternativ lädt Marc das ZIP von Hand hoch.

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
- Ein Paket mit vielen Fotos ist gross. Der Import liest höchstens `Content:MaxPackageBytes` (Standard 2 GB) und legt das Paket dafür in eine temporäre Datei.
- Ändert sich das Format der Versionen, braucht der Import die Formatversion im Manifest.

### Später

- Ein Job lokal, der das Paket z.B. täglich holt. Dafür reicht ein Worker, der `PullContentFromSource` mit einem Admin aufruft.

## Referenzen

- ADR-008 (Versionen), ADR-009 (Fotos)
- `cms.md`, «Inhalt (Admin)»; `infrastructure.md`, «Umgebungen»; `datastructure.md`, «Inhaltspaket»
