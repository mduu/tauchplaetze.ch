# Redaktion (CMS)

Es gibt drei Bereiche (Entscheid Marc, 19.10.2026: «reine Admin-Features auf /platform», wie Feldnah und Bau-Ticker):

| Bereich | Wer | Darstellung |
|---|---|---|
| **Website** mit Bearbeiten-Modus (`/{lang}/tauchplaetze/{slug}/bearbeiten`), «Neuen Platz vorschlagen» (`/{lang}/tauchplaetze/neu`) und «Meine Vorschläge» im Profil | alle Angemeldeten | Layout und Design der Website. Nur diese zwei Seiten sind interaktiv (ADR-001, Nachtrag 19.10.2026). |
| **Moderation** `/admin` | Moderatoren und Admins | MudBlazor, Seitenleiste |
| **Plattform** `/platform` | nur Admins | MudBlazor, Navigation oben, Kopf mit orangem Strich |

Mitwirkende sehen die Moderation nicht (403). Sie bearbeiten und schlagen Plätze nur auf der Website vor. Auch die Versionen eines Platzes im CMS (`/admin/plaetze/{id}/versionen`) sind nur für die Moderation. «Meine Vorschläge» verlinkt auf die öffentliche History des Platzes. Der Editor im CMS bleibt der Moderation, zum Beispiel für die Felder «nur Moderation». Hinweise bearbeiten alle im Bearbeiten-Modus der Website (`ux.md`).

| Pfad | Inhalt | Wer |
|---|---|---|
| `/{lang}/tauchplaetze/{slug}/bearbeiten` | Bearbeiten-Modus: Name und Ort, Bewertungen mit Legende, Texte pro Sprache, Einstiege und Orte, Tauchskizze, Fotos, Kommentar (`ux.md`, «Bearbeiten-Modus») | alle Angemeldeten |
| `/{lang}/tauchplaetze/neu` | Neuen Platz vorschlagen: Name, Gewässer, Koordinate per Karte, Gemeinde, Kanton, Kommentar (`ux.md`, «Neuer Platz») | alle Angemeldeten |
| `/{lang}/account/proposals` | Meine Vorschläge | alle Angemeldeten |
| `/admin` | Übersicht: Plätze, offene Vorschläge | Moderatoren, Admins |
| `/admin/plaetze` | Liste mit Suche, Status, Ausbaustufe, Datenstufe, offenen Vorschlägen | Moderatoren, Admins |
| `/admin/plaetze/neu` | neuer Platz: Name, Gewässer, Koordinate (Klick in die Karte) | Moderatoren, Admins |
| `/admin/plaetze/{id}` | Bearbeiten in Tabs: Grunddaten (mit «Auch bekannt als», Namen mit Komma getrennt), Bewertungen, Texte pro Sprache, Einstiege und Orte, Fotos (Hochladen, Abschnitt, Reihenfolge, Titelbild), Skizze (Editor auf der Karte, `dive-site.md`, «Tauchskizze»), Hinweise (Art, Zeitraum, Text pro Sprache), nur Moderation (Slug, Prognose aus, Satellitenpunkt, BAFU-Station, Schwellen des Abflusses) | Moderatoren, Admins |
| `/admin/plaetze/{id}/versionen` | Versionen mit Vergleich zum Vorgänger, Wiederherstellen | Moderatoren und Admins (Security-Review 20.10.2026: zeigte Mitwirkenden Namen ohne Einwilligung, Begründungen und fremde Vorschläge). Der Core liefert ohne Recht nichts. |
| `/admin/vorschlaege` | offene Vorschläge mit Vergleich zur Basis, Freigeben, Ablehnen mit Begründung | Moderatoren, Admins |
| `/admin/konten` | Konten sperren (ohne E-Mail-Adressen und Rollen) | Moderatoren; Admins nutzen `/platform/konten` |
| `/platform` | Übersicht: Konten (davon gesperrt), offene Vorschläge, letzter Prognose-Lauf, Mail-Queue | Admins |
| `/platform/konten` | Konten, Rollen, Sperre, E-Mail-Adressen | Admins |
| `/platform/konten/{id}` | Ein Konto im Detail (siehe «Konten auf der Plattform») | Admins |
| `/platform/prognose` | Prognose-Läufe mit Fehlern, «Lauf jetzt starten», «Vergangenheit neu rechnen» | Admins |
| `/platform/queues` | Mail-Queue mit Dead-Letter | Admins |

Alte Adressen leiten weiter: `/admin/prognose` → `/platform/prognose` (301), `/admin/meine-vorschlaege` → `/{lang}/account/proposals` (302, die Sprache hängt vom Profil ab). Links zwischen Website, Moderation und Plattform laden die ganze Seite, weil die Bereiche verschiedene Render-Modi und Skripte haben.

Ohne Anmeldung führt `/admin` zur Anmeldung. Fehlt die Rolle, antwortet der Server mit 403.

**Rechte prüft der Core** (Entscheid Marc, 18.10.2026). Jeder Command und jede Query mit geschützten Daten bekommt die Benutzer-ID und liest Rollen und Sperre frisch aus der Datenbank. Die Rollen im Cookie und die Prüfungen der Oberfläche dienen nur der Bedienung. Das Cookie prüft sich alle 5 Minuten gegen die Datenbank (`GetSignedInAccount`): Gibt es das Konto nicht mehr (zum Beispiel nach einem Neuaufbau der Datenbank mit neuer Id), ist es gesperrt oder unbestätigt, meldet die Web-App ab. Sonst übernimmt sie Name, Sprache und Rollen frisch. Ohne Recht liefert ein Command den Fehler `NotAllowed`, bei einem gesperrten Konto immer `Blocked` (die Oberfläche zeigt «Dein Konto ist gesperrt»). Eine Query liefert ohne Recht eine leere Liste.

**Alle können beitragen, Moderatoren geben frei** (Entscheid Marc, 17.10.2026, ADR-008). Wer ein Konto hat, kann Informationen zu Tauchplätzen beisteuern. Online sichtbar wird eine Änderung erst, wenn ein Moderator oder Admin sie freigibt. Jede Änderung ergibt eine Version, wie in einem Wiki. Die History zeigt Autor und Approver.

Zu Beginn pflegt Marc die Inhalte allein. Das Modell mit Freigabe und Versionen gilt aber schon im MVP. Die Registrierung für alle ist gebaut und in Prod offen (`Accounts:RegistrationOpen`, 23.10.2026).

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

Die Stufe misst nur den Fortschritt. Sie blendet nichts aus: Die Platzseite zeigt alles, was erfasst ist, und ein Abschnitt ohne Inhalt fehlt (`ux.md`). Entscheid Marc, 18.10.2026. Der Hinweis «erst im Verzeichnis» hängt nicht an der Stufe, sondern daran, ob überhaupt etwas dokumentiert ist (`ux.md`). Ein Platz mit Texten, aber ohne Einstieg bleibt auf Stufe 0 und zeigt den Hinweis nicht (Fund Marc, Terlinden, 19.10.2026).

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
4. **Freigeben:** Die Version wird `Approved` und ihr Inhalt wird online. Ein neuer Platz wird dabei `Published`.
5. **Ablehnen:** Die Version wird `Rejected`, mit einer kurzen Begründung. Die Autorin oder der Autor sieht sie.
6. **Veraltete Basis:** Wurde seit der Basis eine andere Version freigegeben, zeigt das CMS beide Vergleiche. Der Moderator übernimmt die Änderung von Hand (bearbeitet und gibt frei) oder lehnt ab. Keine automatische Zusammenführung.
7. **Moderatoren und Admins** geben ihre eigenen Änderungen beim Speichern selbst frei. In der History steht dann Autor = Approver (Entscheid Marc, 17.10.2026).
8. Mitwirkende werden per E-Mail über Freigabe oder Ablehnung informiert (siehe «Benachrichtigungen»).
9. Jede Freigabe, jedes Wiederherstellen und jedes Archivieren meldet Platz und Gewässer an IndexNow (`seo.md`, «IndexNow»), gesammelt höchstens alle 10 Minuten. Die Meldung blockiert die Freigabe nie.

### History

- **Öffentlich wie in einem Wiki** (Entscheid Marc, 17.10.2026): Jede Platzseite hat einen Link «Versionen» auf `/{lang}/tauchplaetze/{slug}/versionen`.
- Die öffentliche History zeigt nur freigegebene Versionen: Nummer, Datum, Autor, Approver, Änderungsnotiz.
- Vergleich feldweise mit der vorherigen freigegebenen Version, auch öffentlich (`?v={nummer}`).
- Die Platzseite hat zusätzlich einen Link «Tauchplatz bearbeiten» auf das CMS. Ohne Anmeldung führt er zur Anmeldung.
- Im CMS zeigt die History alle Versionen mit Status, auch `Pending` und `Rejected`, mit den Anzeigenamen.

### Datenschutz in der History

Namen in einer öffentlichen History sind Personendaten (nDSG). Darum gilt **Datenschutz durch Voreinstellungen** (Entscheid Marc, 17.10.2026):

- **Öffentlich anonym als Standard.** Ohne Einwilligung zeigt die öffentliche History «Mitwirkende» statt des Autors und «Moderation» statt des Approvers.
- **Name nur mit Einwilligung.** Im Profil gibt es die Einstellung «Meinen Anzeigenamen öffentlich bei meinen Beiträgen zeigen». Sie ist aus, bis die Person sie einschaltet. Sie gilt für Autor und Approver gleich. Sie wirkt auch rückwirkend: Ausschalten macht alle bisherigen Einträge wieder anonym.
- **Pseudonym erlaubt.** Der Anzeigename muss kein echter Name sein. Die Registrierung sagt das.
- **Nie öffentlich:** E-Mail-Adresse, Konto-ID, IP-Adresse.
- **Nicht in Suchmaschinen:** Die Seiten der History haben `noindex, nofollow` als Meta-Tag und als Header `X-Robots-Tag`, `robots.txt` sperrt sie, und sie stehen nicht in der Sitemap.
- **Schutz gegen Spam (Entscheid Marc, 21.10.2026):** Öffentlich erscheinen nur freigegebene Versionen ohne Schwärzung (`Approved`, `RedactedAt` leer), in der Liste wie im Vergleich. Abgelehnte, wartende und zurückgezogene Versionen erscheinen nie. Kommentare und Änderungen stehen als Text, URLs darin sind nicht verlinkt. So bringt ein abgelehnter Vorschlag mit Links nichts.
- **Konto löschen:** Die Versionen bleiben. Autor bzw. Approver wird «gelöschtes Konto», auch im CMS.
- **Personendaten im Inhalt:** Texte und Fotos können selbst Personendaten enthalten (z.B. Telefonnummern, erkennbare Personen). Moderatoren prüfen das vor der Freigabe. Eine Version mit solchen Daten oder mit Spam lässt sich nachträglich schwärzen (Command `RedactRevision`, Knopf «Schwärzen» mit Rückfrage in der Versionsansicht `/admin/plaetze/{id}/versionen`). Schwärzen dürfen Moderation und Admin, aufheben nur Admin. Die veröffentlichte (jüngste freigegebene) Version lässt sich nicht schwärzen: zuerst eine korrigierte Version freigeben. Eine geschwärzte Version fehlt in der öffentlichen History und im Vergleich. Im CMS bleibt sie mit dem Hinweis «geschwärzt» sichtbar, ihr Inhalt im Blob bleibt für die Moderation lesbar. Zweimal schwärzen ändert nichts. Eine geschwärzte Version lässt sich weder freigeben noch wiederherstellen (Fehler `RedactedRevision`), sonst käme ihr Inhalt über einen Umweg wieder online. Der Knopf «Wiederherstellen» fehlt bei ihr. Einen Grund speichert das System nicht. Das ist die einzige Ausnahme von «Versionen sind unveränderlich».
- Die Datenschutzerklärung beschreibt Konto, Anzeigename, History und Aufbewahrung.

### Konten auf der Plattform

Die Kontenliste verlinkt jedes Konto auf `/platform/konten/{id}` (nur Admins, Query `GetAccountDetails`, sonst «gibt es nicht oder keine Berechtigung»).

- **Angaben:** E-Mail und ob bestätigt, Sprache, Name öffentlich, registriert am, letzte Anmeldung (bei alten Konten «unbekannt» bzw. «noch nie»), Anmeldemethoden (Passwort ja/nein, externe Logins aus Identity, Passkeys später), Fehlversuche und Lockout von Identity, Sperre mit Datum und Person, Beiträge (Versionen, davon offen, Fotos).
- **Rollen:** Schalter pro Rolle (`SetRole`). Die eigene Rolle Admin ist gesperrt.
- **Aktionen**, jede mit Meldung:
  - «E-Mail von Hand bestätigen» (`ConfirmEmailManually`), nur bei unbestätigten Konten.
  - «Link für ein neues Passwort senden» (`SendPasswordReset`, dieselbe Mail wie «Passwort vergessen», in der Sprache des Kontos), nur bei bestätigten, nicht gesperrten Konten.
  - «Sperren» und «Sperre aufheben» (`BlockAccount`), Sperren mit Rückfrage.
  - «Konto löschen» (`DeleteAccountByAdmin`, mit Rückfrage), zum Beispiel für Spam-Konten. Gleiche Regeln wie die Selbstlöschung: Versionen ohne Autor, private Fotos weg, öffentliche ohne Bezug zum Konto. Nie das eigene Konto (das geht im Profil) und nie den letzten aktiven Admin.

### Auskunft und Herausgabe der eigenen Daten

Jede Person hat ein Recht auf Auskunft (nDSG Art. 25) und auf Herausgabe ihrer Daten (Art. 28). Das Konto bietet beides selbst an, ohne Anfrage an uns. Grundlage sind die Seiten von ASP.NET Core Identity unter «Konto verwalten» (`Manage/PersonalData`), erweitert um die Beiträge.

- **Seite «Meine Daten»:** zeigt alle gespeicherten Daten zum Konto:
  - Konto: E-Mail, Anzeigename, Einwilligung zur öffentlichen Anzeige, Sprache, Rollen, registriert am, letzte Anmeldung, Sperre. Passkeys kommen dazu, sobald sie gebaut sind.
  - **Beiträge:** alle Versionen, die die Person geschrieben hat, mit Platz, Datum, Status und Notiz. Jede Zeile hat einen Link auf die Version bzw. den Vergleich.
  - **Freigaben:** alle Versionen, die die Person freigegeben oder abgelehnt hat (nur Moderatoren und Admins), mit Link.
  - **Fotos:** alle hochgeladenen Fotos mit Platz, Status und Link.
- **Download:** «Meine Daten herunterladen» liefert alles als JSON. Die Datei enthält die Links auf die Beiträge und die Texte der eigenen Notizen und Begründungen. Den Inhalt der Versionen liefert sie als Link, nicht als Kopie. Felder am Benutzer tragen dafür `[PersonalData]`.
- **Konto löschen:** wie oben unter «Konto löschen». Die Seite sagt vorher, was bleibt (Versionen ohne Namen) und was gelöscht wird (Konto, E-Mail, Anzeigename, Fotos im Status `Pending`).
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
- Nicht: archivieren, Slugs ändern, Prognose ausschalten, Satellitenpunkt setzen, Gewässer und Stationen ändern, Schwellen des Abflusses setzen. Der Core übernimmt diese Felder bei Vorschlägen von Mitwirkenden aus der aktuellen Version (`Proposals.ProtectModeratorFields`).

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
- Button «Automatisch übersetzen» in jedem Sprach-Tab, im CMS und im Bearbeiten-Modus der Website. Details im nächsten Abschnitt.
- Speichern ergibt eine neue Version (siehe «Mitwirken und Freigabe»). Bei Moderatoren und Admins ist sie sofort freigegeben.
- Optimistische Sperre über `RowVersion`. Hat jemand anders zwischenzeitlich eine Version freigegeben, erscheint ein Hinweis mit Vergleich.

### Automatisch übersetzen

Dienst: Claude API mit dem Glossar aus `ubiquitous-language.md` als Kontext (Entscheid 17.10.2026). Limite und Budget: Entscheid Marc, 22.10.2026.

- Jeder Sprach-Tab hat den Button «Automatisch übersetzen». Er übersetzt alle Texte des Platzes aus einer Quellsprache mit Text in die Sprache des Tabs. Quelle: Deutsch, sonst die erste Sprache mit Text. Bei mehreren Sprachen mit Text wählt man die Quelle.
- Hat der Tab schon Text, fragt der Editor zuerst («Die Texte in dieser Sprache werden ersetzt.»).
- Das Ergebnis steht nur im Formular. Gespeichert wird wie sonst, als Vorschlag oder direkt freigegeben. Die Texte haben `IsMachineTranslated = true`.
- Der Core übernimmt `IsMachineTranslated` beim Speichern nur, wenn der Text unverändert ist oder genau einer gespeicherten Übersetzung entspricht (`MachineTranslations.ResultHash`). Sonst setzt er das Flag zurück.
- Die Claude API läuft nur auf Knopfdruck, nie beim Laden einer Seite. Während der Übersetzung ist der Button inaktiv. Zwei gleichzeitige gleiche Anfragen kosten trotzdem zweimal, das nehmen wir in Kauf.
- Command `TranslateSiteText`, Query `GetTranslationAllowance` (Slice `Translations`). Der Button fehlt ohne Recht oder ohne Schlüssel. Bei Limit oder Budget steht er inaktiv da, mit Hinweis.

**Schutz gegen Missbrauch und Kosten:**

- Der Schlüssel der Claude API liegt nur auf dem Server (`Translation:ApiKey`, Key Vault). Ohne Schlüssel ist die Funktion aus.
- Kein freier Text: nur die sechs Textfelder eines Platzes mit ihren Längen (Unter Wasser 8000 Zeichen, Gefahren, Anfahrt und Umgebung je 2000, Restaurants und Toiletten 1000, Tauchverbot 500). Nur DE, EN, FR und IT.
- Nur bestätigte, nicht gesperrte Konten.
- Tageslimit pro Konto und Schweizer Tag: Mitwirkende 50, Moderation 500, Admins ohne Limit (`Translation:DailyLimitContributor`, `Translation:DailyLimitModerator`).
- Budget pro Monat für alle zusammen: 5 USD (`Translation:MonthlyBudgetUsd`). Die Kosten rechnet der Core aus den Tokens der Antworten und den Preisen in `Translation:InputPricePerMillion` und `Translation:OutputPricePerMillion`. Zusätzlich gilt ein Spend-Limit im Workspace der Claude Console.
- Cache: Gleicher Quelltext, gleiche Sprachen, gleiches Modell und gleicher Prompt ergeben dieselbe Übersetzung ohne neuen Aufruf. Ein Treffer zählt nicht für das Limit.
- Eine bezahlte Übersetzung zählt für Limit und Budget, auch wenn sie unbrauchbar ist.

**Modell und Markdown:**

- Modell: `claude-haiku-5-5` (`Translation:Model`), Effort `low`. Kosten am 22.10.2026: 0.10 USD pro Million Input-Tokens, 0.50 USD pro Million Output-Tokens. Ein Platz in drei Sprachen kostet etwa 0.01 USD.
- Die Antwort ist JSON mit denselben Feldern wie die Anfrage (Structured Outputs). Der Prompt behandelt die Texte nur als Daten.
- Der Core prüft das Markdown jedes Felds gegen die Quelle: Zwischentitel mit Ebene, Listenpunkte, Absätze und Fett (`MarkdownStructure`). Weicht etwas ab oder fehlt ein Feld, verwirft er die Übersetzung («Die Übersetzung hat nicht geklappt»).

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

Abschnitt «Fotos» im Bearbeiten-Modus und Tab «Fotos» im Editor der Moderation (ADR-009). Beide nutzen dieselben Commands.

- **Hochladen:** bis 20 Fotos auf einmal (JPEG, PNG oder WebP, je höchstens 25 MB, höchstens 12 000 px pro Seite und 50 Megapixel; HEIC bitte als JPEG exportieren). Das Format kommt aus dem Inhalt der Datei, Endung und MIME-Type sind nur ein Vorfilter. Pflicht: Fotograf (Vorgabe: Anzeigename) und die Bestätigung «Ich habe diese Fotos selbst gemacht oder besitze die Rechte daran und erlaube tauchplaetze.ch, sie mit Namensnennung zu zeigen». Optional: Link zum Fotografen, Abschnitt, Aufnahmetiefe. Bevor die Oberfläche eine Datei liest, fragt sie den Core, ob und wie viele Fotos das Konto noch hochladen darf (Query `GetUploadAllowance`: gesperrt, verbleibend, Moderation ohne Limit). Wählt jemand mehr, kommt keine Datei hoch. Meldet der Core unterwegs `TooManyUploads` oder `Blocked`, bricht der Upload ab. Bricht die Verbindung beim Lesen ab, erscheint ein Fehler. Gilt für Bearbeiten-Modus und CMS (`MediaUpload`).
- **Verarbeitung im Core** (Command `UploadMedia`): immer nur ein Bild gleichzeitig, grosse JPEGs verkleinert dekodiert (1/2, 1/4, 1/8), Drehen nach EXIF, Aufnahmedatum aus EXIF, Bildgrössen 480, 960, 1600 und 2400 px Breite als WebP (nie grösser als das Original). Die Bildgrössen tragen keine Metadaten, also weder Standort noch Kamera.
- **Privat bis zur Freigabe:** Original und Bildgrössen liegen in `media-pending/{mediaId}/`. Die Vorschau im CMS läuft über `/admin/medien/{id}/{datei}`. Sie zeigt ein privates Foto nur der Person, die es hochgeladen hat, und der Moderation.
- **Link zum Fotografen:** nur absolute `http`- oder `https`-Adressen, sonst `InvalidPhotographerUrl`.
- **Zuordnung ist Teil der Version:** `DiveSiteContent.Media` (Foto, Abschnitt, bei «Einstieg» der Einstieg; die Reihenfolge der Liste ist die Reihenfolge) und `HeroMediaId` (Titelbild). Der Einstieg ist optional: Ein Foto im Abschnitt «Einstieg» darf ohne bestimmten Einstieg bleiben, zum Beispiel wenn der Platz noch keinen Einstieg hat. Ein Foto eines anderen Platzes, doppelt, ein Titelbild ausserhalb der Liste, ein Einstieg ausserhalb des Abschnitts «Einstieg» oder ein Einstieg, den es nicht mehr gibt, ergibt `InvalidMedia`. Mitwirkende verwenden nur öffentliche oder eigene Fotos. Gelöschte Fotos fallen beim Speichern, Freigeben und Wiederherstellen still aus der Version, ein fehlendes Titelbild ebenso.
- **Freigabe:** Gibt die Moderation eine Version frei (auch beim direkten Speichern, beim Wiederherstellen und beim Erstimport), kopiert der Core die Bildgrössen der verwendeten Fotos nach `media/{mediaId}/{breite}.webp` und setzt `IsPublic`. Ein schon öffentliches Foto wird nicht nochmals kopiert. Fotos, die die Version nicht verwendet, verlieren ihren Platz in der Galerie (`SortOrder = null`). Das Original wird nie öffentlich.
- **Angaben ändern** (Command `UpdateMedia`): Fotograf, Link, Tiefe, Legende und Alternativtext pro Sprache. Bei privaten Fotos die Person, die hochgeladen hat, bei öffentlichen nur die Moderation. Diese Angaben sind nicht Teil der Version.
- **Löschen** (Command `DeleteMedia`, Knopf «Löschen» mit Bestätigung; im Bearbeiten-Modus fragt ein zweiter Klick nach, ohne Dialog des Browsers): Die Moderation darf das immer, die Person, die hochgeladen hat, solange das Foto privat ist. Gelöscht werden der Eintrag und alle Dateien in `media-pending` und `media`; ein Titelbild darauf fällt weg. Versionen, die das Foto noch verwenden, zeigen es nicht mehr. **In Prod:** Cloudflare hält die Bildgrössen bis zu einem Jahr im Cache (`immutable`). Nach dem Löschen eines öffentlichen Fotos den Cache für `/{mediaId}/` von Hand leeren (Purge).
- **Aufräumen** (Command `PurgeUnusedMedia`, täglich 03:30 Lokalzeit im Web-Host, Lock `locks/media-purge`): Ein privates Foto, das älter als 30 Tage ist (`Media:PurgeAfterDays`) und kein offener Vorschlag verwendet, verschwindet samt Dateien. Öffentliche Fotos und Fotos in offenen Vorschlägen bleiben.
- **Reihenfolge** mit Pfeilen hoch und runter. Ein Foto als Titelbild wählen (Stern).
- **Schutz:** höchstens 30 Uploads pro Stunde und 100 pro Tag und Konto (Moderation ausgenommen). Ein gesperrtes Konto lädt nichts hoch.
- Import aus dem eigenen Fotoarchiv: Marc lädt die Originale hoch. Kein automatischer Abzug von einer anderen Website.

### Seen, Shops, Buddy-Gruppen

- Shops und Buddy-Gruppen sind geplant (Phase 1), es gibt sie noch nicht.
- Einfache Listen mit Bearbeiten-Dialog.
- Seen enthalten zusätzlich die Alplakes-Zuordnung (`AlplakesLakeKey`, Modellnamen). Nur Admins ändern diese Felder.

### Meldungen (`/admin/meldungen`)

- Kommentare, die auf Freigabe warten (älteste zuerst): «Freigeben» oder «Ablehnen» (Kommentar weg, Sicht und Temperatur bleiben).
- Die 50 neuesten Meldungen mit «Ausblenden» und «Wieder zeigen», für Admins zusätzlich «Löschen» mit Rückfrage. Regeln in `dive-site.md`, «Meldungen».

### Plattform (Admin)

- `/platform/queues`, nur Admin, Navigation der Plattform.
- Zähler: Mails in der Queue, Mails in der Dead-Letter-Queue (ungefähr).
- Liste der Dead-Letter-Mails (höchstens 32, nur diese einzeln bearbeitbar): Betreff, Empfänger, Vorlage, Versuche, letzter Fehler, Zeit, Mail-Id. Unlesbare Nachrichten erscheinen als «Unlesbare Nachricht». Dead-Letter-Mails löscht Azure nach 30 Tagen (Datenschutz).
- Hinweis: Konto-Mails tragen Links mit Ablauf. Erneut gesendet, können sie ungültig sein.
- Erneut senden: eine oder alle. Sie beginnen wieder beim ersten Versuch.
- Löschen: eine oder alle, mit Bestätigung. Endgültig.
- Alle Aktionen prüfen die Rolle im Core (`GetMailQueueStatus`, `ReplayDeadLetters`, `DeleteDeadLetters`).

### Inhalt (Admin)

- `/platform/inhalt`, nur Admin, Navigation der Plattform (ADR-011).
- «Inhalt exportieren»: Download des Inhaltspakets als ZIP. Die Seite zeigt, ob der Abruf mit Schlüssel eingeschaltet ist.
- «Inhalt übernehmen» nur mit `Content:ImportEnabled` (lokal): «Aus Prod übernehmen» (nur mit `Content:SourceBaseUri` und `Content:ExportKey`) oder ein ZIP hochladen.
- Ergebnis: Gewässer neu, Plätze neu, aktualisiert, unverändert, übersprungen (unlesbar, ungültig, Slug belegt), Fotos neu, Fotos übersprungen.
- Die Rolle und den Schalter prüft der Core (`ExportContent`, `ImportContent`, `PullContentFromSource`, `GetContentSyncStatus`).

### Prognose-Status

- Query `GetForecastRunOverview` (nur Admins, sonst `null`): letzter Lauf, nächster Lauf nach `ForecastSchedule`, Bilanz der letzten 7 Tage, mittlere Dauer, eine Seite Läufe (optional nur mit Fehlern). Die gescheiterten Calls gruppiert der Core nach Quelle und Art (`FailureKind`: HTTP mit Status-Code, Timeout, Circuit Breaker offen, Antwort unlesbar, Netzwerk), mit Anzahl, Keys und Meldungen.
- Fortschritt eines laufenden Laufs (Plätze fertig, Calls bisher) hält der Core nur im Speicher des Prozesses (`ForecastProgress`). Die Calls speichert der Lauf erst am Ende. Ein fertiger Lauf speichert die Zahl seiner Plätze (`Sites`, ältere Läufe ohne Wert). Steht ein Lauf seit mehr als 2 Stunden auf «läuft» und hat in diesem Prozess keinen Fortschritt, zeigt die Übersicht ihn als «abgebrochen» (nicht gespeichert); die Seite fragt dann nicht mehr alle 5 s nach.
- Button «Lauf jetzt starten» (nur Admin). Er ruft `StartForecastRun`, das die Rolle prüft. Der Worker ruft `RunForecast` direkt.
- Button «Vergangenheit neu rechnen» (nur Admin, mit Rückfrage). Er ruft `RecomputeForecastHistory`: gespeicherte Tage mit älterer Regel neu rechnen, Median pro Gewässer neu aufbauen, ohne externe Calls (`forecast.md`, «Verlauf»). Danach meldet die Seite, wie viele Tage neu gerechnet wurden und wie viele davon einen anderen Index haben. Läuft gerade ein Prognose-Lauf, startet nichts.

## Rollen

| Rolle | Darf |
|---|---|
| `Contributor` (Mitwirkende) | Änderungen und neue Plätze vorschlagen, Fotos hochladen, eigene Vorschläge sehen und zurückziehen |
| `Moderator` | alles von `Contributor`, dazu Vorschläge freigeben oder ablehnen, eigene Änderungen direkt freigeben, archivieren, Konten sperren (ausser Admin-Konten), Meldungen ausblenden |
| `Admin` | alles, dazu Rollen vergeben, Admin-Konten sperren, Gewässer und Stationen zuordnen, Prognose-Lauf starten |

- Anmeldung mit ASP.NET Core Identity: E-Mail plus Passwort. Passkeys sind geplant.
- **Selbstregistrierung** für Mitwirkende (Entscheid Marc, 17.10.2026): E-Mail bestätigen, Anzeigename wählen (Pseudonym erlaubt). Öffentlich erscheint der Anzeigename nur mit Einwilligung (siehe «Datenschutz in der History»). Gebaut. Ohne `Accounts:RegistrationOpen = true` ist sie zu (Standard). In Prod offen seit 23.10.2026 (Entscheid Marc, App Setting in `deploy/modules/webApp.bicep`). Lokal startet Aspire mit offener Registrierung.
- **Schutz der Konten** (Security-Review, 20.10.2026):
  - **Keine Konto-Enumeration:** Die Registrierung prüft Anzeigename und Passwort zuerst, bei bekannter und neuer Adresse gleich. Danach meldet eine bekannte Adresse denselben Erfolg wie eine neue. Die Mail sagt «Du hast schon ein Konto», bei einem unbestätigten Konto kommt die Bestätigung noch einmal. Der bekannte Zweig rechnet einen Passwort-Hash wie ein neues Konto, damit die Antwortzeit nichts verrät. Die Registrierung sendet sofort (ADR-010), weil beide Zweige eine Mail senden. «Passwort vergessen» sendet über die Queue (5–30 s), weil nur der bekannte Zweig eine Mail hat und ein sofortiger Versand die Antwortzeit verlängern und so ein Konto verraten würde. Ein gesperrtes Konto (Lockout) bekommt bei der Anmeldung dieselbe Antwort wie ein falsches Passwort. «Passwort vergessen» antwortet immer gleich.
  - **Bots:** Honeypot und Zeitfalle wie beim Kontaktformular (`FormBotTrap`): ein unsichtbares Feld und ein signierter Zeitstempel. Feld gefüllt oder unter 3 s gesendet: Der Bot sieht «Fast geschafft», es entsteht kein Konto und keine Mail. Älter als 6 h: «Formular abgelaufen». Dazu das Rate Limit der Konto-Formulare (`infrastructure.md`) und die Bestätigung per Mail: Ein unbestätigtes Konto kann sich nicht anmelden. Ist die Adresse 7 Tage nach der Registrierung noch unbestätigt, löscht der Command `DeleteUnconfirmedAccounts` das Konto (täglich 03:45 Lokalzeit im Web-Host, Entscheid Marc 23.10.2026). Die Adresse ist danach wieder frei.
  - **Lockout:** nach 5 Fehlversuchen 15 Minuten, nicht eskalierend. Eine Eskalation würde das Sperren fremder Konten (DoS) verschärfen. Dagegen hilft das Rate Limit pro Client.
  - **Abkühlzeit:** höchstens eine Mail derselben Art pro Konto in 15 Minuten (Bestätigung oder «Konto besteht schon», neues Passwort), nur im Arbeitsspeicher.
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
