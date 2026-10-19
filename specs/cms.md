# Redaktion (CMS)

Es gibt drei Bereiche (Entscheid Marc, 19.10.2026: «reine Admin-Features auf /platform», wie Feldnah und Bau-Ticker):

| Bereich | Wer | Darstellung |
|---|---|---|
| **Website** mit Bearbeiten-Modus (`/{lang}/tauchplaetze/{slug}/bearbeiten`) und «Meine Vorschläge» im Profil | alle Angemeldeten | Layout und Design der Website. Nur die Bearbeiten-Seite ist interaktiv (ADR-001, Nachtrag 19.10.2026). |
| **Moderation** `/admin` | Moderatoren und Admins | MudBlazor, Seitenleiste |
| **Plattform** `/platform` | nur Admins | MudBlazor, Navigation oben, Kopf mit orangem Strich |

Mitwirkende sehen die Moderation nicht. Bis Einstiege, Skizze und Fotos im Bearbeiten-Modus stehen (Etappen 2 und 3), bleibt ihnen nur der Editor eines Platzes (`/admin/plaetze/{id}`), der neue Platz (`/admin/plaetze/neu`) und die Versionen eines Platzes offen. Die Navigation der Moderation zeigt ihnen dort nur «Meine Vorschläge».

| Pfad | Inhalt | Wer |
|---|---|---|
| `/{lang}/tauchplaetze/{slug}/bearbeiten` | Bearbeiten-Modus: Name und Ort, Bewertungen mit Legende, Texte pro Sprache, Kommentar (`ux.md`, «Bearbeiten-Modus») | alle Angemeldeten |
| `/{lang}/account/proposals` | Meine Vorschläge | alle Angemeldeten |
| `/admin` | Übersicht: Plätze, offene Vorschläge | Moderatoren, Admins |
| `/admin/plaetze` | Liste mit Suche, Status, Ausbaustufe, Datenstufe, offenen Vorschlägen | Moderatoren, Admins |
| `/admin/plaetze/neu` | neuer Platz: Name, Gewässer, Koordinate (Klick in die Karte) | alle Angemeldeten |
| `/admin/plaetze/{id}` | Bearbeiten in Tabs: Grunddaten (mit «Auch bekannt als», Namen mit Komma getrennt), Bewertungen, Texte pro Sprache, Einstiege und Orte, Fotos (Hochladen, Abschnitt, Reihenfolge, Titelbild), Skizze (Editor auf der Karte, `dive-site.md`, «Tauchskizze»), Hinweise (Art, Zeitraum, Text pro Sprache), nur Moderation (Slug, Prognose aus, Satellitenpunkt, BAFU-Station) | alle Angemeldeten, Tab «nur Moderation» für Moderatoren |
| `/admin/plaetze/{id}/versionen` | Versionen mit Vergleich zum Vorgänger, Wiederherstellen | alle Angemeldeten, Wiederherstellen für Moderatoren |
| `/admin/vorschlaege` | offene Vorschläge mit Vergleich zur Basis, Freigeben, Ablehnen mit Begründung | Moderatoren, Admins |
| `/admin/konten` | Konten sperren (ohne E-Mail-Adressen und Rollen) | Moderatoren; Admins nutzen `/platform/konten` |
| `/platform` | Übersicht: Konten (davon gesperrt), offene Vorschläge, letzter Prognose-Lauf, Mail-Queue | Admins |
| `/platform/konten` | Konten, Rollen, Sperre, E-Mail-Adressen | Admins |
| `/platform/prognose` | Prognose-Läufe mit Fehlern, «Lauf jetzt starten» | Admins |
| `/platform/queues` | Mail-Queue mit Dead-Letter | Admins |

Alte Adressen leiten weiter (301): `/admin/prognose` → `/platform/prognose`, `/admin/meine-vorschlaege` → `/{lang}/account/proposals`. Links zwischen Website, Moderation und Plattform laden die ganze Seite, weil die Bereiche verschiedene Render-Modi und Skripte haben.

Ohne Anmeldung führt `/admin` zur Anmeldung. Fehlt die Rolle, antwortet der Server mit 403.

**Rechte prüft der Core** (Entscheid Marc, 18.10.2026). Jeder Command und jede Query mit geschützten Daten bekommt die Benutzer-ID und liest Rollen und Sperre frisch aus der Datenbank. Die Rollen im Cookie und die Prüfungen der Oberfläche dienen nur der Bedienung. Ohne Recht liefert ein Command den Fehler `NotAllowed`, bei einem gesperrten Konto immer `Blocked` (die Oberfläche zeigt «Dein Konto ist gesperrt»). Eine Query liefert ohne Recht eine leere Liste.

**Alle können beitragen, Moderatoren geben frei** (Entscheid Marc, 17.10.2026, ADR-008). Wer ein Konto hat, kann Informationen zu Tauchplätzen beisteuern. Online sichtbar wird eine Änderung erst, wenn ein Moderator oder Admin sie freigibt. Jede Änderung ergibt eine Version, wie in einem Wiki. Die History zeigt Autor und Approver.

Zu Beginn pflegt Marc die Inhalte allein. Das Modell mit Freigabe und Versionen gilt aber schon im MVP. In Phase 1 öffnen wir die Registrierung für alle.

## Grundsatz: Verzeichnis zuerst

Ein Tauchplatz ist mit **Name, Koordinate und Gewässer** erfasst. Ab dann nimmt er an der Prognose teil, so genau, wie die Daten es erlauben. Alle anderen Felder sind optional. So kann Marc zuerst alle bekannten Plätze schnell anlegen und sie danach in Ruhe ausbauen.

Die **Ausbaustufe** misst die Dokumentation. Die **Datenstufe** misst die Daten der Prognose (`forecast.md`). Der Job berechnet sie. Die Redaktion kann die Prognose nur ausschalten und den Satellitenpunkt setzen.

## Ausbaustufen

Der Core berechnet die Ausbaustufe bei jeder Speicherung (`DiveSite.CalculateContentLevel()`). Die Regel steht an einer Stelle.

| Stufe | Bedingung |
|---|---|
| 0 – Verzeichnis | Name, Koordinate, See |
| 1 – Basis | Stufe 0 plus `MaxDepth`, `DifficultyRating`, `Access`, ein Einstieg mit Beschreibung (DE) und `ApproachInfo` (DE) |
| 2 – Vollständig | Stufe 1 plus `UnderwaterDescription` (DE) und ein Hero-Foto |

Die Stufe misst nur den Fortschritt. Sie blendet nichts aus: Die Platzseite zeigt alles, was erfasst ist, und ein Abschnitt ohne Inhalt fehlt (`ux.md`). Nur Stufe 0 zeigt zusätzlich den Hinweis «erst im Verzeichnis». Entscheid Marc, 18.10.2026.

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
8. Mitwirkende werden per E-Mail über Freigabe oder Ablehnung informiert (siehe «Benachrichtigungen»).

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

### Benachrichtigungen

Mails gehen über die Mail-Queue (ADR-010), formatiert mit gemeinsamem Layout und Textfassung, in der Sprache der Empfängerin. Empfänger sind nur aktive Konten (E-Mail bestätigt, nicht gesperrt), nie die Person, die gehandelt hat (Entscheid Marc, 19.10.2026).

| Ereignis | Empfänger | Inhalt |
|---|---|---|
| Mitwirkende schlagen eine Änderung oder einen neuen Platz vor | alle Moderatoren und Admins | Platz, Autorin, Kommentar, Link auf «Vorschläge» |
| Ein Vorschlag wird freigegeben | Autorin oder Autor | Link auf den Platz |
| Ein Vorschlag wird abgelehnt | Autorin oder Autor | Begründung als Zitat |
| Die Moderation ändert einen Platz direkt (speichern, neuer Platz, wiederherstellen) | alle übrigen Admins, solange `Notifications:NotifyAdminsOnModeratorChanges` gilt (Standard an, gedacht für die Anfangsphase) | Platz, Version, Link auf die Versionen |

Konto-Mails (Bestätigung, Passwort) und das Kontaktformular nutzen dasselbe Layout.

**Keine Mail beim Einreichen** (Entscheid Marc, 19.10.2026): Mitwirkende erfahren im CMS, dass ihr Vorschlag angekommen ist. Eine Mail bekommen sie erst bei Freigabe oder Ablehnung.

### Was Mitwirkende sehen

- **Nach dem Speichern:** «Danke! Dein Vorschlag ist bei der Moderation. Du bekommst eine Mail, sobald er freigegeben oder abgelehnt ist.» Im Bearbeiten-Modus führt das Speichern zurück auf die Platzseite, die das als Banner zeigt.
- **Meine Vorschläge** (`/{lang}/account/proposals`, im Profil der Website, für alle Angemeldeten): eigene Vorschläge, neuste zuerst, mit Platz, Version, Datum, Status (wartet, freigegeben, abgelehnt mit Begründung, zurückgezogen), eigenem Kommentar und Link auf Platz und Versionen. Wartende lassen sich zurückziehen. Eigene Änderungen, die Moderatoren selbst freigegeben haben, erscheinen nicht. Query `GetMyProposals`.
- **Beim Öffnen eines Platzes** (Bearbeiten-Modus und Editor): Hat die Person dort einen offenen Vorschlag, erscheint «Du hast hier einen offenen Vorschlag vom {Datum}» mit Link auf «Meine Vorschläge».

### Was Mitwirkende dürfen

- Texte, Bewertungen, Einstiege und Lage eines bestehenden Platzes ändern.
- Einen neuen Platz vorschlagen (Name, Koordinate, Gewässer).
- Fotos hochladen. Dabei bestätigen sie, dass sie die Rechte haben und tauchplaetze.ch das Foto mit Namensnennung zeigen darf. Ein Foto ist erst nach der Freigabe der Version öffentlich, die es verwendet.
- Nicht: archivieren, Slugs ändern, Prognose ausschalten, Satellitenpunkt setzen, Gewässer und Stationen ändern.

### Schutz gegen Missbrauch

- Höchstens 20 offene Vorschläge pro Konto, konfigurierbar.
- Rate Limit beim Speichern und beim Upload.
- Moderatoren können ein Konto sperren. Dessen offene Vorschläge werden `Rejected`. Ein Admin-Konto sperren nur Admins (Entscheid Marc, 18.10.2026). Niemand sperrt sich selbst.
- **Gesperrt heisst: ab sofort nichts mehr schreiben** (Entscheid Marc, 18.10.2026). Der Core lehnt jeden schreibenden Command des Kontos ab: vorschlagen, zurückziehen, freigeben, ablehnen, wiederherstellen, archivieren, sperren, Rollen vergeben, Profil ändern, Prognose-Lauf starten. Lesen bleibt bis zur Abmeldung möglich: CMS-Seiten, «Meine Daten» und der Download. Das Konto löschen bleibt erlaubt (Recht auf Löschung). Eine neue Anmeldung lehnt der Core ab.
- Fotos von Mitwirkenden liegen bis zur Freigabe in einem privaten Container.

## Funktionen

### Platzliste

- Tabelle mit Name, See, Ausbaustufe, Datenstufe, Status, letzter Änderung.
- Filter nach See, Stufe und Status. Suche nach Name.
- Schnellerfassung: Name, See, Pin auf der Karte, Speichern. Unter 30 Sekunden pro Platz.

### Platz bearbeiten

- Abschnitte: Grunddaten, Lage (Karte mit verschiebbarem Pin für den Platz, optional ein zweiter Pin für den Satellitenpunkt seewärts, Schalter «Prognose ausschalten»), Einstiege, Bewertungen, Eigenschaften, Texte pro Sprache (Tabs DE, EN, FR, IT), Fotos, Vorschau.
- Einstiege: Liste mit Hinzufügen, Entfernen und Reihenfolge. Pro Einstieg ein Pin auf derselben Karte, Art, Name und Beschreibung pro Sprache.
- Bewertungen: pro Skala eine Auswahl 1–5 mit Kurztext der Stufe und «nicht bewertet». Skalen in `dive-site.md`.
- Texte pro Sprache: Unter Wasser, Gefahren, Anfahrt und Parkplatz, Umgebung, Restaurants und Toiletten.
- Texte: Deutsch ist Pflicht. EN/FR/IT sind optional.
- Button «Fehlende Sprachen übersetzen» (Phase 1, optional): erzeugt einen Vorschlag per LLM und setzt `IsMachineTranslated = true`. Eine Person prüft und speichert. Dienst: Claude API mit dem Glossar aus `ubiquitous-language.md` als Kontext (Entscheid 17.10.2026).
- Speichern ergibt eine neue Version (siehe «Mitwirken und Freigabe»). Bei Moderatoren und Admins ist sie sofort freigegeben.
- Optimistische Sperre über `RowVersion`. Hat jemand anders zwischenzeitlich eine Version freigegeben, erscheint ein Hinweis mit Vergleich.

### Erstimport

- Die ersten Plätze stehen in `Features/Editorial/Seed/dive-sites.json` (Name, weitere Namen, Gewässer, Koordinate, Gemeinde, Kanton, Startwerte aus OSM, Einstiege). Weitere Namen werden beim Import normalisiert, ungültige fallen weg.
- `ImportDiveSites` legt nur fehlende Plätze an. Bestehende ändert er nie. Kommt im Seed etwas zu einem bestehenden Platz dazu (weitere Namen, Startwerte, ein entfernter Parkplatz-Ort), trägt die Redaktion es im CMS nach, oder die Datenbank wird neu aufgesetzt (lokal). Jeder bekommt eine freigegebene Version 1 mit der Notiz «Erstimport». Autor und Approver ist der erste Admin aus `Accounts:Admins`.
- Der Slug kommt aus Name und Gemeinde (`riff-williams-herrliberg`). Ein Platz mit vorhandenem Slug bleibt unverändert. Ein zweiter Lauf ändert nichts.
- Lokal läuft der Import beim Start der Web-App (`Database:PrepareOnStartup`).

### Vorschläge prüfen (Moderatoren)

- Liste der offenen Vorschläge, älteste zuerst, mit Platz, Autor, Notiz und Alter der Basis.
- Vergleich zur Basis und, falls veraltet, zur aktuellen Version.
- **Fotos im Vergleich** (Query `CompareRevisionMedia`, auch in «Versionen»): Vorschaubild, Abschnitt, Titelbild, Fotograf und Legende jedes Fotos der Version. Neue und geänderte Fotos (anderer Abschnitt, Einstieg, Platz in der Reihenfolge oder Titelbild) haben einen orangen Rahmen (`--color-signal`), entfernte sind ausgegraut und durchgestrichen. Private Fotos zeigen ihr Vorschaubild nur der Moderation und der Person, die sie hochgeladen hat.
- Aktionen: Freigeben, Bearbeiten und freigeben, Ablehnen mit Begründung.

### Versionen

- Liste aller Versionen mit Status, Autor, Approver, Zeit und Notiz.
- Vergleich zweier Versionen feldweise.
- «Wiederherstellen» erzeugt eine neue Version mit dem alten Inhalt.

### Medien

Tab «Fotos» beim Bearbeiten eines Platzes (ADR-009).

- **Hochladen:** mehrere Fotos auf einmal (JPEG, PNG oder WebP, je höchstens 25 MB, höchstens 12 000 px pro Seite und 50 Megapixel; HEIC bitte als JPEG exportieren). Das Format kommt aus dem Inhalt der Datei, Endung und MIME-Type sind nur ein Vorfilter. Pflicht: Fotograf (Vorgabe: Anzeigename) und die Bestätigung «Ich habe diese Fotos selbst gemacht oder besitze die Rechte daran und erlaube tauchplaetze.ch, sie mit Namensnennung zu zeigen». Optional: Link zum Fotografen, Abschnitt, Aufnahmetiefe.
- **Verarbeitung im Core** (Command `UploadMedia`): immer nur ein Bild gleichzeitig, grosse JPEGs verkleinert dekodiert (1/2, 1/4, 1/8), Drehen nach EXIF, Aufnahmedatum aus EXIF, Bildgrössen 480, 960, 1600 und 2400 px Breite als WebP (nie grösser als das Original). Die Bildgrössen tragen keine Metadaten, also weder Standort noch Kamera.
- **Privat bis zur Freigabe:** Original und Bildgrössen liegen in `media-pending/{mediaId}/`. Die Vorschau im CMS läuft über `/admin/medien/{id}/{datei}`. Sie zeigt ein privates Foto nur der Person, die es hochgeladen hat, und der Moderation.
- **Link zum Fotografen:** nur absolute `http`- oder `https`-Adressen, sonst `InvalidPhotographerUrl`.
- **Zuordnung ist Teil der Version:** `DiveSiteContent.Media` (Foto, Abschnitt, bei «Einstieg» der Einstieg; die Reihenfolge der Liste ist die Reihenfolge) und `HeroMediaId` (Titelbild). Ein Foto eines anderen Platzes, doppelt, ein Titelbild ausserhalb der Liste oder ein fehlender Einstieg ergibt `InvalidMedia`. Mitwirkende verwenden nur öffentliche oder eigene Fotos. Gelöschte Fotos fallen beim Speichern, Freigeben und Wiederherstellen still aus der Version, ein fehlendes Titelbild ebenso.
- **Freigabe:** Gibt die Moderation eine Version frei (auch beim direkten Speichern, beim Wiederherstellen und beim Erstimport), kopiert der Core die Bildgrössen der verwendeten Fotos nach `media/{mediaId}/{breite}.webp` und setzt `IsPublic`. Ein schon öffentliches Foto wird nicht nochmals kopiert. Fotos, die die Version nicht verwendet, verlieren ihren Platz in der Galerie (`SortOrder = null`). Das Original wird nie öffentlich.
- **Angaben ändern** (Command `UpdateMedia`): Fotograf, Link, Tiefe, Legende und Alternativtext pro Sprache. Bei privaten Fotos die Person, die hochgeladen hat, bei öffentlichen nur die Moderation. Diese Angaben sind nicht Teil der Version.
- **Löschen** (Command `DeleteMedia`, Knopf «Löschen» mit Bestätigung): Die Moderation darf das immer, die Person, die hochgeladen hat, solange das Foto privat ist. Gelöscht werden der Eintrag und alle Dateien in `media-pending` und `media`; ein Titelbild darauf fällt weg. Versionen, die das Foto noch verwenden, zeigen es nicht mehr. **In Prod:** Cloudflare hält die Bildgrössen bis zu einem Jahr im Cache (`immutable`). Nach dem Löschen eines öffentlichen Fotos den Cache für `/{mediaId}/` von Hand leeren (Purge).
- **Aufräumen** (Command `PurgeUnusedMedia`, täglich 03:30 Lokalzeit im Web-Host, Lock `locks/media-purge`): Ein privates Foto, das älter als 30 Tage ist (`Media:PurgeAfterDays`) und kein offener Vorschlag verwendet, verschwindet samt Dateien. Öffentliche Fotos und Fotos in offenen Vorschlägen bleiben.
- **Reihenfolge** mit Pfeilen hoch und runter. Ein Foto als Titelbild wählen (Stern).
- **Schutz:** höchstens 30 Uploads pro Stunde und 100 pro Tag und Konto (Moderation ausgenommen). Ein gesperrtes Konto lädt nichts hoch.
- Import aus tauchfotos.ch: Marc lädt die Originale hoch. Kein automatischer Abzug von der alten Website.

### Seen, Shops, Buddy-Gruppen

- Einfache Listen mit Bearbeiten-Dialog.
- Seen enthalten zusätzlich die Alplakes-Zuordnung (`AlplakesLakeKey`, Modellnamen). Nur Admins ändern diese Felder.

### Meldungen (Phase 1)

- Liste der neuen Meldungen. Aktion «Ausblenden» bei Missbrauch.

### Plattform (Admin)

- `/platform/queues`, nur Admin, Navigation der Plattform.
- Zähler: Mails in der Queue, Mails in der Dead-Letter-Queue (ungefähr).
- Liste der Dead-Letter-Mails (höchstens 32, nur diese einzeln bearbeitbar): Betreff, Empfänger, Vorlage, Versuche, letzter Fehler, Zeit, Mail-Id. Unlesbare Nachrichten erscheinen als «Unlesbare Nachricht». Dead-Letter-Mails löscht Azure nach 30 Tagen (Datenschutz).
- Hinweis: Konto-Mails tragen Links mit Ablauf. Erneut gesendet, können sie ungültig sein.
- Erneut senden: eine oder alle. Sie beginnen wieder beim ersten Versuch.
- Löschen: eine oder alle, mit Bestätigung. Endgültig.
- Alle Aktionen prüfen die Rolle im Core (`GetMailQueueStatus`, `ReplayDeadLetters`, `DeleteDeadLetters`).

### Prognose-Status

- Letzter Lauf, Status pro Quelle, Datenalter, Fehler.
- Button «Lauf jetzt starten» (nur Admin). Er ruft `StartForecastRun`, das die Rolle prüft. Der Worker ruft `RunForecast` direkt.

## Rollen

| Rolle | Darf |
|---|---|
| `Contributor` (Mitwirkende) | Änderungen und neue Plätze vorschlagen, Fotos hochladen, eigene Vorschläge sehen und zurückziehen |
| `Moderator` | alles von `Contributor`, dazu Vorschläge freigeben oder ablehnen, eigene Änderungen direkt freigeben, archivieren, Konten sperren (ausser Admin-Konten), Meldungen ausblenden |
| `Admin` | alles, dazu Rollen vergeben, Admin-Konten sperren, Gewässer und Stationen zuordnen, Prognose-Lauf starten |

- Anmeldung mit ASP.NET Core Identity: E-Mail plus Passkey oder Passwort.
- **Selbstregistrierung** für Mitwirkende (Entscheid Marc, 17.10.2026): E-Mail bestätigen, Anzeigename wählen (Pseudonym erlaubt). Öffentlich erscheint der Anzeigename nur mit Einwilligung (siehe «Datenschutz in der History»). Ab Phase 1 offen, im MVP ausgeschaltet (`Accounts:RegistrationOpen`).
- Admins ernennen Moderatoren.
- **Der letzte aktive Admin bleibt** (Entscheid Marc, 18.10.2026). Sperren, die Rolle Admin wegnehmen und das Konto löschen lehnt der Core mit `LastAdmin` ab, wenn danach kein Admin ohne Sperre übrig wäre.
- Löscht jemand sein Konto, bleiben die Versionen. Autor bzw. Approver wird «gelöschtes Konto». Das erfüllt das Recht auf Löschung und hält die History vollständig.

## Slices im Code

- `Features/Editorial/Commands`: `ProposeDiveSite`, `ProposeChange`, `WithdrawRevision`, `ApproveRevision`, `RejectRevision`, `ArchiveDiveSite`, `RestoreRevision`, `UploadMedia`.
- `Features/Editorial/Queries`: `GetDiveSitesForEditing`, `GetDiveSiteForEditing`, `GetPendingRevisions`, `GetRevisions`, `CompareRevisions`, `GetMyContributions`.
- `Features/Accounts/Queries`: `GetPersonalData` (Seite «Meine Daten», Download und Auskunft durch einen Admin). Eine Query für alle drei Wege.
- `Features/DiveSites/Queries`: `GetPublicRevisions`, `ComparePublicRevisions` für die öffentliche History.
- `ProposeChange` eines Moderators ruft intern dieselbe Regel wie `ApproveRevision`. Die Regel steht an einer Stelle.
- Die öffentlichen Abfragen liegen im Slice `DiveSites` (`GetDiveSite`, `GetDiveSites`), nicht im Slice `Editorial`.
