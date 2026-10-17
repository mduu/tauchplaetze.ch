# Redaktion (CMS)

Der Redaktionsbereich liegt unter `/admin`. Er ist ein Teil der Blazor Web App mit dem Render-Modus **Interactive Server** (ADR-001). Zu Beginn pflegt Marc die Inhalte allein. Später kommen einzelne Redaktoren dazu.

## Grundsatz: Verzeichnis zuerst

Ein Tauchplatz ist mit **Name, Koordinate und Gewässer** erfasst. Ab dann nimmt er an der Prognose teil, so genau, wie die Daten es erlauben. Alle anderen Felder sind optional. So kann Marc zuerst alle bekannten Plätze schnell anlegen und sie danach in Ruhe ausbauen.

Die **Ausbaustufe** misst die Dokumentation. Die **Datenstufe** misst die Daten der Prognose (`forecast.md`). Der Job berechnet sie. Die Redaktion kann die Prognose nur ausschalten und den Satellitenpunkt setzen.

## Ausbaustufen

Der Core berechnet die Ausbaustufe bei jeder Speicherung (`DiveSite.CalculateContentLevel()`). Die Regel steht an einer Stelle.

| Stufe | Bedingung | Sichtbar auf der Platzseite |
|---|---|---|
| 0 – Verzeichnis | Name, Koordinate, See | Prognose, Karte, Hinweis «erst im Verzeichnis» |
| 1 – Basis | Stufe 0 plus `MaxDepth`, `DifficultyRating`, `Access`, ein Einstieg mit Beschreibung (DE) und `ApproachInfo` (DE) | zusätzlich Praktisch-Block, Bewertungen und Tags |
| 2 – Vollständig | Stufe 1 plus `UnderwaterDescription` (DE) und ein Hero-Foto | zusätzlich Unter Wasser, Galerie, Skizze (falls vorhanden) |

Die Platzliste im CMS zeigt die Stufe als Badge. So ist sichtbar, wo noch Arbeit wartet.

## Status

- `Draft`: nur im CMS sichtbar.
- `Published`: öffentlich. Auch ein Platz der Stufe 0 darf veröffentlicht sein.
- `Archived`: nicht mehr öffentlich, URL liefert 410 (oder Redirect auf den See).

## Funktionen

### Platzliste

- Tabelle mit Name, See, Ausbaustufe, Datenstufe, Status, letzter Änderung.
- Filter nach See, Stufe und Status. Suche nach Name.
- Schnellerfassung: Name, See, Pin auf der Karte, Speichern. Unter 30 Sekunden pro Platz.

### Platz bearbeiten

- Abschnitte: Grunddaten, Lage (Karte mit verschiebbarem Pin für den Platz, optional ein zweiter Pin für den Satellitenpunkt seewärts, Schalter «Prognose ausschalten»), Einstiege, Bewertungen, Eigenschaften, Texte pro Sprache (Tabs DE, EN, FR, IT), Medien, Vorschau.
- Einstiege: Liste mit Hinzufügen, Entfernen und Reihenfolge. Pro Einstieg ein Pin auf derselben Karte, Art, Name und Beschreibung pro Sprache.
- Bewertungen: pro Skala eine Auswahl 1–5 mit Kurztext der Stufe und «nicht bewertet». Skalen in `dive-site.md`.
- Texte pro Sprache: Unter Wasser, Gefahren, Anfahrt und Parkplatz, Umgebung, Restaurants und Toiletten.
- Texte: Deutsch ist Pflicht. EN/FR/IT sind optional.
- Button «Fehlende Sprachen übersetzen» (Phase 1, optional): erzeugt einen Vorschlag per LLM und setzt `IsMachineTranslated = true`. Ein Redaktor prüft und speichert. Dienst: Claude API mit dem Glossar aus `ubiquitous-language.md` als Kontext (Entscheid 17.10.2026).
- Optimistische Sperre über `RowVersion`. Hat jemand anders zwischenzeitlich gespeichert, erscheint ein Hinweis mit Vergleich.

### Revisionen

- Jede Speicherung erzeugt eine `DiveSiteRevision` mit JSON-Snapshot.
- Ansicht: Liste der Revisionen mit Autor, Zeit, Notiz.
- Vergleich zweier Revisionen feldweise.
- «Wiederherstellen» erzeugt eine neue Revision mit dem alten Inhalt. Nichts wird überschrieben.

### Medien

- Upload mehrerer Fotos auf einmal (Drag and Drop).
- Der Core liest EXIF (Datum, falls vorhanden) und erzeugt Bildgrössen (z.B. 480, 960, 1600, 2400 px Breite) als WebP und JPEG.
- Original und Grössen liegen in Blob Storage (`media/{mediaId}/{size}.webp`).
- Pflicht: Fotograf. Optional: Legende und Alternativtext pro Sprache, Aufnahmetiefe.
- Jedes Bild eines Platzes einem Abschnitt zuordnen (Unter Wasser, Umgebung, Anfahrt, Einstieg, Gefahren). Bei «Einstieg» zusätzlich den Einstieg wählen.
- Reihenfolge per Drag and Drop innerhalb des Abschnitts. Ein Foto als Hero wählen.
- Import aus tauchfotos.ch: Marc lädt die Originale hoch. Kein automatischer Abzug von der alten Website.

### Seen, Shops, Buddy-Gruppen

- Einfache Listen mit Bearbeiten-Dialog.
- Seen enthalten zusätzlich die Alplakes-Zuordnung (`AlplakesLakeKey`, Modellnamen). Nur Admins ändern diese Felder.

### Meldungen (Phase 1)

- Liste der neuen Meldungen. Aktion «Ausblenden» bei Missbrauch.

### Prognose-Status

- Letzter Lauf, Status pro Quelle, Datenalter, Fehler.
- Button «Lauf jetzt starten» (nur Admin).

## Rollen

| Rolle | Darf |
|---|---|
| `Editor` | Plätze und Medien bearbeiten, Meldungen ausblenden |
| `Admin` | alles, dazu Benutzer, Seen-Zuordnung, Prognose-Lauf starten |

Anmeldung mit ASP.NET Core Identity (E-Mail plus Passwort, dazu Passkeys wie bei feldnah). Keine Selbstregistrierung. Admins laden Redaktoren ein.

## Slices im Code

- `Features/Editorial/Commands`: `CreateDiveSite`, `UpdateDiveSite`, `PublishDiveSite`, `ArchiveDiveSite`, `RestoreRevision`, `UploadMedia`, `UpdateMedia`, `ReorderMedia`, `DeleteMedia`.
- `Features/Editorial/Queries`: `GetDiveSitesForEditing`, `GetDiveSiteForEditing`, `GetRevisions`, `CompareRevisions`.
- Die öffentlichen Abfragen liegen im Slice `DiveSites` (`GetDiveSite`, `GetDiveSites`), nicht im Slice `Editorial`.
