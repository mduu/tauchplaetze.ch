# Redaktion (CMS)

Der Redaktionsbereich liegt unter `/admin`. Er ist ein Teil der Blazor Web App mit dem Render-Modus **Interactive Server** (ADR-001). Nur Pfade unter `/admin` sind interaktiv. Die öffentlichen Seiten bleiben Static SSR.

Die Oberfläche nutzt **MudBlazor** im dunklen Theme aus den Design Tokens. Seiten:

| Pfad | Inhalt | Wer |
|---|---|---|
| `/admin` | Übersicht: Plätze, offene Vorschläge, letzter Prognose-Lauf | alle Angemeldeten |
| `/admin/plaetze` | Liste mit Suche, Status, Ausbaustufe, Datenstufe, offenen Vorschlägen | alle Angemeldeten |
| `/admin/plaetze/neu` | neuer Platz: Name, Gewässer, Koordinate (Klick in die Karte) | alle Angemeldeten |
| `/admin/plaetze/{id}` | Bearbeiten in Tabs: Grunddaten, Bewertungen, Texte pro Sprache, Einstiege, nur Moderation (Slug, Prognose aus, Satellitenpunkt, BAFU-Station) | alle Angemeldeten, Tab «nur Moderation» für Moderatoren |
| `/admin/plaetze/{id}/versionen` | Versionen mit Vergleich zum Vorgänger, Wiederherstellen | alle Angemeldeten, Wiederherstellen für Moderatoren |
| `/admin/vorschlaege` | offene Vorschläge mit Vergleich zur Basis, Freigeben, Ablehnen mit Begründung | Moderatoren, Admins |
| `/admin/prognose` | Prognose-Läufe mit Fehlern, «Lauf jetzt starten» | Moderatoren; starten nur Admins |
| `/admin/konten` | Konten, Rollen, Sperre | Admins |

Ohne Anmeldung führt `/admin` zur Anmeldung. Fehlt die Rolle, antwortet der Server mit 403.

**Alle können beitragen, Moderatoren geben frei** (Entscheid Marc, 17.10.2026, ADR-008). Wer ein Konto hat, kann Informationen zu Tauchplätzen beisteuern. Online sichtbar wird eine Änderung erst, wenn ein Moderator oder Admin sie freigibt. Jede Änderung ergibt eine Version, wie in einem Wiki. Die History zeigt Autor und Approver.

Zu Beginn pflegt Marc die Inhalte allein. Das Modell mit Freigabe und Versionen gilt aber schon im MVP. In Phase 1 öffnen wir die Registrierung für alle.

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

## Status eines Platzes

- `Draft`: Der Platz hat noch keine freigegebene Version. Nur im CMS sichtbar. Ein neuer Platz von Mitwirkenden startet so.
- `Published`: öffentlich. Auch ein Platz der Stufe 0 darf veröffentlicht sein.
- `Archived`: nicht mehr öffentlich, URL liefert 410 (oder Redirect auf das Gewässer). Nur Moderatoren und Admins archivieren.

## Mitwirken und Freigabe

### Versionen

- **Jede Speicherung eines Platzes ergibt eine Version** (`DiveSiteRevision`). Sie enthält den ganzen Platz: Grunddaten, Lage, Einstiege, Bewertungen, Texte in allen Sprachen und die Zuordnung der Medien.
- Eine Version hat einen **Status:**

  | Status | Bedeutung |
  |---|---|
  | `Pending` | Änderungsvorschlag, wartet auf Freigabe. Nicht öffentlich. |
  | `Approved` | freigegeben. Die neueste freigegebene Version ist online. |
  | `Rejected` | abgelehnt, mit Begründung. Nicht öffentlich. |
  | `Withdrawn` | von der Autorin oder vom Autor zurückgezogen. |

- **Autor und Approver:** Jede Version speichert, wer sie geschrieben hat und wer sie freigegeben oder abgelehnt hat, mit Zeit und Notiz.
- **Speicherort:** Die Metadaten einer Version stehen in SQL. Der Inhalt (Snapshot als JSON) liegt in Blob Storage (Entscheid Marc, 17.10.2026). Die aktuelle freigegebene Version steht zusätzlich in den normalen Tabellen. Die öffentlichen Seiten lesen nur diese Tabellen. Alte Versionen lädt nur die History aus Blob Storage.
- Versionen sind unveränderlich (Ausnahme: Schwärzung, siehe «Datenschutz in der History»). «Wiederherstellen» erzeugt eine neue Version mit dem alten Inhalt. Nichts wird überschrieben oder gelöscht.

### Ablauf

1. Mitwirkende bearbeiten einen Platz oder schlagen einen neuen Platz vor. Das Speichern ergibt eine Version `Pending` mit Änderungsnotiz.
2. Die Version merkt sich ihre **Basis**: die freigegebene Version, auf der die Änderung aufbaut.
3. Moderatoren sehen eine Liste der offenen Vorschläge mit dem Vergleich zur Basis.
4. **Freigeben:** Die Version wird `Approved` und ihr Inhalt wird online. Der Output Cache des Platzes wird ungültig. Ein neuer Platz wird dabei `Published`.
5. **Ablehnen:** Die Version wird `Rejected`, mit einer kurzen Begründung. Die Autorin oder der Autor sieht sie.
6. **Veraltete Basis:** Wurde seit der Basis eine andere Version freigegeben, zeigt das CMS beide Vergleiche. Der Moderator übernimmt die Änderung von Hand (bearbeitet und gibt frei) oder lehnt ab. Keine automatische Zusammenführung.
7. **Moderatoren und Admins** geben ihre eigenen Änderungen beim Speichern selbst frei. In der History steht dann Autor = Approver (Entscheid Marc, 17.10.2026).
8. Mitwirkende werden per E-Mail über Freigabe oder Ablehnung informiert (Phase 1).

### History

- **Öffentlich wie in einem Wiki** (Entscheid Marc, 17.10.2026): Jede Platzseite hat einen Link «Versionen» auf `/{lang}/tauchplaetze/{slug}/versionen`.
- Die öffentliche History zeigt nur freigegebene Versionen: Nummer, Datum, Autor, Approver, Änderungsnotiz.
- Vergleich feldweise mit der vorherigen freigegebenen Version, auch öffentlich (`?v={nummer}`).
- Die Platzseite hat zusätzlich einen Link «Platz bearbeiten» auf das CMS. Ohne Anmeldung führt er zur Anmeldung.
- Im CMS zeigt die History alle Versionen mit Status, auch `Pending` und `Rejected`, mit den Anzeigenamen.

### Datenschutz in der History

Namen in einer öffentlichen History sind Personendaten (nDSG). Darum gilt **Datenschutz durch Voreinstellungen** (Entscheid Marc, 17.10.2026):

- **Öffentlich anonym als Standard.** Ohne Einwilligung zeigt die öffentliche History «Mitwirkende» statt des Autors und «Moderation» statt des Approvers.
- **Name nur mit Einwilligung.** Im Profil gibt es die Einstellung «Meinen Anzeigenamen öffentlich bei meinen Beiträgen zeigen». Sie ist aus, bis die Person sie einschaltet. Sie gilt für Autor und Approver gleich. Sie wirkt auch rückwirkend: Ausschalten macht alle bisherigen Einträge wieder anonym.
- **Pseudonym erlaubt.** Der Anzeigename muss kein echter Name sein. Die Registrierung sagt das.
- **Nie öffentlich:** E-Mail-Adresse, Konto-ID, IP-Adresse.
- **Nicht in Suchmaschinen:** Die Seiten der History haben `noindex` und stehen nicht in der Sitemap.
- **Konto löschen:** Die Versionen bleiben. Autor bzw. Approver wird «gelöschtes Konto», auch im CMS.
- **Personendaten im Inhalt:** Texte und Fotos können selbst Personendaten enthalten (z.B. Telefonnummern, erkennbare Personen). Moderatoren prüfen das vor der Freigabe. Eine Version mit solchen Daten lässt sich im Inhalt nachträglich schwärzen. Das ist die einzige Ausnahme von «Versionen sind unveränderlich». Die Schwärzung selbst steht in der History.
- Die Datenschutzerklärung beschreibt Konto, Anzeigename, History und Aufbewahrung.

### Auskunft und Herausgabe der eigenen Daten

Jede Person hat ein Recht auf Auskunft (nDSG Art. 25) und auf Herausgabe ihrer Daten (Art. 28). Das Konto bietet beides selbst an, ohne Anfrage an uns. Grundlage sind die Seiten von ASP.NET Core Identity unter «Konto verwalten» (`Manage/PersonalData`), erweitert um die Beiträge.

- **Seite «Meine Daten»:** zeigt alle gespeicherten Daten zum Konto:
  - Konto: E-Mail, Anzeigename, Einwilligung zur öffentlichen Anzeige, Sprache, Rollen, registriert am, letzte Anmeldung, Passkeys (Name, angelegt am), Sperre.
  - **Beiträge:** alle Versionen, die die Person geschrieben hat, mit Platz, Datum, Status und Notiz. Jede Zeile hat einen Link auf die Version bzw. den Vergleich.
  - **Freigaben:** alle Versionen, die die Person freigegeben oder abgelehnt hat (nur Moderatoren und Admins), mit Link.
  - **Fotos:** alle hochgeladenen Fotos mit Platz, Status und Link.
- **Download:** «Meine Daten herunterladen» liefert alles als JSON. Die Datei enthält die Links auf die Beiträge und die Texte der eigenen Notizen und Begründungen. Den Inhalt der Versionen liefert sie als Link, nicht als Kopie. Felder am Benutzer tragen dafür `[PersonalData]`.
- **Konto löschen:** wie oben unter «Konto löschen». Die Seite sagt vorher, was bleibt (Versionen ohne Namen) und was gelöscht wird (Konto, E-Mail, Passkeys, Anzeigename, Fotos im Status `Pending`).
- **Anfrage per E-Mail:** Kommt eine Anfrage ohne Konto oder per Post, erzeugt ein Admin denselben Bericht im CMS (Konto suchen, «Auskunft erstellen»). Antwort innerhalb von 30 Tagen. Jede erstellte Auskunft steht im Log des Kontos.
- **Logs:** Application Insights speichert IP-Adressen nicht. Die Aufbewahrung der Logs steht in `infrastructure.md`.

### Was Mitwirkende dürfen

- Texte, Bewertungen, Einstiege und Lage eines bestehenden Platzes ändern.
- Einen neuen Platz vorschlagen (Name, Koordinate, Gewässer).
- Fotos hochladen. Dabei bestätigen sie, dass sie die Rechte haben und tauchplaetze.ch das Foto mit Namensnennung zeigen darf. Ein Foto ist erst nach der Freigabe der Version öffentlich, die es verwendet.
- Nicht: archivieren, Slugs ändern, Prognose ausschalten, Satellitenpunkt setzen, Gewässer und Stationen ändern.

### Schutz gegen Missbrauch

- Höchstens 20 offene Vorschläge pro Konto, konfigurierbar.
- Rate Limit beim Speichern und beim Upload.
- Moderatoren können ein Konto sperren. Dessen offene Vorschläge werden `Rejected`.
- Fotos von Mitwirkenden liegen bis zur Freigabe in einem privaten Container.

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
- Button «Fehlende Sprachen übersetzen» (Phase 1, optional): erzeugt einen Vorschlag per LLM und setzt `IsMachineTranslated = true`. Eine Person prüft und speichert. Dienst: Claude API mit dem Glossar aus `ubiquitous-language.md` als Kontext (Entscheid 17.10.2026).
- Speichern ergibt eine neue Version (siehe «Mitwirken und Freigabe»). Bei Moderatoren und Admins ist sie sofort freigegeben.
- Optimistische Sperre über `RowVersion`. Hat jemand anders zwischenzeitlich eine Version freigegeben, erscheint ein Hinweis mit Vergleich.

### Erstimport

- Die ersten Plätze stehen in `Features/Editorial/Seed/dive-sites.json` (Name, Gewässer, Koordinate, Gemeinde, Kanton).
- `ImportDiveSites` legt fehlende Plätze an. Jeder bekommt eine freigegebene Version 1 mit der Notiz «Erstimport». Autor und Approver ist der erste Admin aus `Accounts:Admins`.
- Der Slug kommt aus Name und Gemeinde (`riff-williams-herrliberg`). Ein Platz mit vorhandenem Slug bleibt unverändert. Ein zweiter Lauf ändert nichts.
- Lokal läuft der Import beim Start der Web-App (`Database:PrepareOnStartup`).

### Vorschläge prüfen (Moderatoren)

- Liste der offenen Vorschläge, älteste zuerst, mit Platz, Autor, Notiz und Alter der Basis.
- Vergleich zur Basis und, falls veraltet, zur aktuellen Version.
- Aktionen: Freigeben, Bearbeiten und freigeben, Ablehnen mit Begründung.

### Versionen

- Liste aller Versionen mit Status, Autor, Approver, Zeit und Notiz.
- Vergleich zweier Versionen feldweise.
- «Wiederherstellen» erzeugt eine neue Version mit dem alten Inhalt.

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
| `Contributor` (Mitwirkende) | Änderungen und neue Plätze vorschlagen, Fotos hochladen, eigene Vorschläge sehen und zurückziehen |
| `Moderator` | alles von `Contributor`, dazu Vorschläge freigeben oder ablehnen, eigene Änderungen direkt freigeben, archivieren, Konten sperren, Meldungen ausblenden |
| `Admin` | alles, dazu Rollen vergeben, Gewässer und Stationen zuordnen, Prognose-Lauf starten |

- Anmeldung mit ASP.NET Core Identity: E-Mail plus Passkey oder Passwort.
- **Selbstregistrierung** für Mitwirkende (Entscheid Marc, 17.10.2026): E-Mail bestätigen, Anzeigename wählen (Pseudonym erlaubt). Öffentlich erscheint der Anzeigename nur mit Einwilligung (siehe «Datenschutz in der History»). Ab Phase 1 offen, im MVP ausgeschaltet (`Accounts:RegistrationOpen`).
- Admins ernennen Moderatoren.
- Löscht jemand sein Konto, bleiben die Versionen. Autor bzw. Approver wird «gelöschtes Konto». Das erfüllt das Recht auf Löschung und hält die History vollständig.

## Slices im Code

- `Features/Editorial/Commands`: `ProposeDiveSite`, `ProposeChange`, `WithdrawRevision`, `ApproveRevision`, `RejectRevision`, `ArchiveDiveSite`, `RestoreRevision`, `UploadMedia`.
- `Features/Editorial/Queries`: `GetDiveSitesForEditing`, `GetDiveSiteForEditing`, `GetPendingRevisions`, `GetRevisions`, `CompareRevisions`, `GetMyContributions`.
- `Features/Accounts/Queries`: `GetPersonalData` (Seite «Meine Daten», Download und Auskunft durch einen Admin). Eine Query für alle drei Wege.
- `Features/DiveSites/Queries`: `GetPublicRevisions`, `ComparePublicRevisions` für die öffentliche History.
- `ProposeChange` eines Moderators ruft intern dieselbe Regel wie `ApproveRevision`. Die Regel steht an einer Stelle.
- Die öffentlichen Abfragen liegen im Slice `DiveSites` (`GetDiveSite`, `GetDiveSites`), nicht im Slice `Editorial`.
