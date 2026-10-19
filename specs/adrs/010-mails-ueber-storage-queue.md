# ADR-010: Mails über eine Storage Queue mit Dead-Letter

## Status

**Akzeptiert** (19.10.2026)

## Kontext

- Die App schickt Mails: Konto bestätigen, Passwort, Kontaktformular und neu Benachrichtigungen der Redaktion (`cms.md`, «Benachrichtigungen»).
- Bisher sendete jeder Command direkt per SMTP. Fällt der Mail-Server aus, scheitert der Command oder die Mail geht verloren, ohne dass es jemand merkt.
- Marc wünscht den Ablauf «analog Feldnah»: Queue, Dead-Lettering, sichtbar unter `/platform` (Entscheid 19.10.2026).
- Betrieb unter CHF 20 pro Monat, ein Storage Account ist schon da (`infrastructure.md`).

## Entscheidung

Wir entscheiden uns für **Azure Storage Queues** im bestehenden Storage Account: `mail-jobs` und `mail-jobs-deadletter`.

- **Umschlag:** Die Queue trägt einen `MailEnvelope` (Vorlage, Modell als JSON, Empfänger, Sprache, Reply-To, `MessageId`). Gerendert wird erst beim Senden. Nur Modelle mit einer Vorlage im Code kommen in die Queue (Whitelist, wie Feldnah).
- **Vorlagen:** Bausteine (`MailContent`: Anrede, Absätze, Knopf, Zitat) statt Razor-Komponenten. Ein Layout (`MailLayout`) macht daraus HTML (Tabelle, Inline-CSS, hell) und die Textfassung. Grund: Der Core ist kein Razor-Projekt. Mit Bausteinen sehen alle Mails gleich aus, und die Textfassung entsteht ohne Umweg über HTML. Texte in `MailTexts.resx` in vier Sprachen, Sprache aus dem Konto.
- **Worker:** `MailQueueWorker` ruft den Command `ProcessMailQueue` (bis 16 Mails, Sichtbarkeit 2 min). Leer: 5 s warten.
- **Wiederholen:** Nach einem Fehler wird die Mail erst nach 1, 5, 15 bzw. 60 Minuten wieder sichtbar (Backoff über die Sichtbarkeit). Der fünfte Fehler legt sie in die Dead-Letter-Queue.
- **Sofort Dead-Letter** bei Fehlern, die kein neuer Versuch behebt: unlesbarer Umschlag, unbekannte Vorlage, ungültige Adresse, SMTP-Antwort 5xx.
- **Dead-Letter** mit Umschlag, Zahl der Versuche, letztem Fehler und Zeit, ohne Ablauf (Storage Queues löschen sonst nach 7 Tagen still).
- **Konto-Mails** (Bestätigung, Passwort) sendet der Core sofort (2 Versuche). Scheitert das, gehen sie in die Queue.
- **Plattform:** `/platform/queues` (nur Admin) zeigt die Zähler, die Dead-Letter-Mails (höchstens 32, Grenze von Peek) mit Fehler, sendet einzeln oder alle erneut, löscht einzeln oder alle.
- **Einzelaktionen:** Storage Queues finden keine Nachricht über ihre Id. Der Core empfängt die Dead-Letter-Queue in Paketen (Sichtbarkeit 30 s), handelt bei der passenden `MessageId` und gibt alle übrigen sofort wieder frei. Bei wenigen Dead-Letter-Mails ist das schnell genug.
- **Überwachung:** `DeadLetterMonitorWorker` ruft alle 15 Minuten `CheckDeadLetters`. Liegt etwas in der Dead-Letter-Queue, schreibt der Core einen Fehler ins Log («Deadletter queue … awaiting investigation»). Darauf hört in Prod ein Alert (`infrastructure.md`).

## Optionen

### Option A: Direkt per SMTP (bisher)

**Vorteile:** einfach. **Nachteile:** Ausfall des Mail-Servers lässt Commands scheitern oder Mails verloren gehen, nichts ist sichtbar.

### Option B: Outbox-Tabelle in Azure SQL

**Vorteile:** in derselben Transaktion wie die Änderung, nichts geht verloren. **Nachteile:** Polling belastet die Free-Offer-Datenbank (vCore-Sekunden, Auto-Pause wird verhindert). Weicht von Feldnah ab.

### Option C: Azure Service Bus

**Vorteile:** echtes Dead-Lettering, Sessions. **Nachteile:** eigene Ressource und Kosten, für einige Mails pro Tag zu gross.

### Option D: Storage Queue (gewählt)

**Vorteile:** im bestehenden Storage Account, Kosten im Rappenbereich, übersteht Neustarts, wie Feldnah. **Nachteile:** kein natives Dead-Lettering, keine Suche nach Id, Zähler nur ungefähr.

## Konsequenzen

### Positive Auswirkungen

- Ein Ausfall des Mail-Servers verzögert Mails nur. Commands scheitern nicht mehr daran.
- Alle Mails haben dasselbe Layout und eine Textfassung, in der Sprache der Empfängerin.
- Dead-Letter-Mails sind sichtbar und lassen sich erneut senden.

### Negative Auswirkungen / Risiken

- **Keine Outbox:** Der Command speichert zuerst und reiht danach ein. Scheitert das Einreihen, loggt der Core einen Fehler, und die Mail fehlt. Bewusst in Kauf genommen (Option B).
- **At-least-once:** Gelingt der Versand, aber das Löschen nicht, kommt die Mail ein zweites Mal. Für Benachrichtigungen vertretbar.
- Der Worker pollt alle 5 s: rund 520 000 Transaktionen pro Monat, Kosten wenige Rappen.

## Referenzen

- Feldnah ADR-010 (Storage Queue), ADR-013 (Monitoring über Log-Signal)
- `cms.md`, «Benachrichtigungen» und «Plattform»; `infrastructure.md`, «Storage Queues»
