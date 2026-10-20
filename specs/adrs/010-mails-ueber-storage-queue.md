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
- **Worker:** `MailQueueWorker` ruft den Command `ProcessMailQueue`: 4 Mails pro Durchgang, Sichtbarkeit 5 min, SMTP-Timeout 20 s pro Befehl. So ist das Paket gesendet, bevor eine Mail wieder sichtbar wird. Ist die Queue leer, wartet der Worker 5 s und verdoppelt bis 30 s. Ein Fehler beendet die Schleife nie. Die Queues legt der Worker beim Start an (Command `CreateMailQueues`); lesende Queries legen nichts an.
- **Nach dem Versand:** Die Mail wird nur noch gelöscht, nie zurückgestellt. Scheitert das Löschen (abgelaufener Pop Receipt), loggt der Core das, und die Mail kann nach der Sichtbarkeit doppelt kommen. Ein Fehler beim Trennen von SMTP nach dem Senden wird nur geloggt.
- **Wiederholen:** Nach einem Fehler wird die Mail erst nach 1, 5, 15 bzw. 60 Minuten wieder sichtbar (Backoff über die Sichtbarkeit). Der fünfte Fehler legt sie in die Dead-Letter-Queue.
- **Sofort Dead-Letter** nur bei Fehlern, die kein neuer Versuch behebt: unlesbarer Umschlag, unbekannte Vorlage, ungültige Adresse (Parse-Fehler), Empfänger vom Server abgelehnt (`RecipientNotAccepted`). Andere 5xx (Absender, Anmeldung, Richtlinie) sind oft Konfiguration, sie bekommen wie 4xx und Timeouts den Backoff.
- **Dead-Letter** mit Umschlag, Zahl der Versuche, letztem Fehler und Zeit. Sie laufen nach `Mail:DeadLetterRetentionDays` ab (Standard 30 Tage), weil der Umschlag Adressen und Inhalte trägt (Datenschutz).
- **Logs ohne Personendaten:** Im Log stehen nur Mail-Id, Vorlage, Typ und Code des Fehlers. Der volle Fehlertext steht nur in der Dead-Letter-Mail, die nur Admins sehen.
- **Konto-Mails** (Bestätigung, Passwort) sendet der Core sofort (2 Versuche). Scheitert das, gehen sie in die Queue.
- **Plattform:** `/platform/queues` (nur Admin) zeigt die Zähler, die Dead-Letter-Mails (höchstens 32, Grenze von Peek) mit Fehler, sendet einzeln oder alle erneut, löscht einzeln oder alle.
- **Einzelaktionen:** Storage Queues finden keine Nachricht über ihre Id. Der Core empfängt die Dead-Letter-Queue in Paketen von 32 (Sichtbarkeit 2 min), handelt bei der passenden `MessageId` und gibt alle übrigen wieder frei, auch nach einem Fehler. Die Seite zeigt nur die ersten 32 (Grenze von Peek); nur diese lassen sich einzeln bearbeiten. «Alle» bearbeitet höchstens so viele, wie beim Start da waren.
- **Konto-Mails beim erneuten Senden:** Ihre Links (Bestätigung, Passwort) laufen ab. Die Seite weist darauf hin.
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
- **At-least-once:** Gelingt der Versand, aber das Löschen nicht, kommt die Mail ein zweites Mal. Ebenso beim erneuten Senden aus der Dead-Letter-Queue: zuerst einreihen, dann löschen. Scheitert das Löschen, liegt die Mail in beiden Queues. Für Benachrichtigungen vertretbar.
- Der Worker pollt höchstens alle 5 s, bei leerer Queue alle 30 s: unter 520 000 Transaktionen pro Monat, rund 20 Rappen.

## Referenzen

- Feldnah ADR-010 (Storage Queue), ADR-013 (Monitoring über Log-Signal)
- `cms.md`, «Benachrichtigungen» und «Plattform»; `infrastructure.md`, «Storage Queues»

## Nachtrag 20.10.2026 (Security-Review)

«Passwort vergessen» sendet über die Queue statt sofort. Nur eine bekannte Adresse löst eine Mail aus. Ein sofortiger Versand würde die Antwort um die Zeit des SMTP-Versands verlängern, und das verriete, dass es das Konto gibt. Die Queue liefert in 5–30 s. Der Link «neues Passwort» aus der Plattform (Admin) und die Registrierung senden weiter sofort.
