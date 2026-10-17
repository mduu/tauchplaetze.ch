# ADR-008: Beiträge mit Freigabe, Versionen in Blob Storage

## Status

**Akzeptiert** (17.10.2026)

## Kontext

- Bisher plante das CMS nur eingeladene Redaktoren. Sie speichern direkt.
- Marc will, dass alle Informationen zu Tauchplätzen beisteuern können. Online sichtbar wird ein Beitrag erst nach der Freigabe durch einen Moderator oder Admin.
- Zu jeder Änderung soll eine Versionshistory bestehen, wie in einem Wiki. Sie zeigt Autor und Approver.
- Namen in einer öffentlichen History sind Personendaten (nDSG).
- Die Datenbank ist das Azure SQL Free Offer mit 32 GB. Ein JSON-Snapshot pro Version wächst mit jeder Änderung.

## Entscheidung

Wir entscheiden uns für:

1. **Jede Speicherung ergibt eine Version** mit Status `Pending`, `Approved`, `Rejected` oder `Withdrawn`. Die neueste freigegebene Version ist online.
2. **Selbstregistrierung für Mitwirkende.** Moderatoren und Admins geben Vorschläge frei. Ihre eigenen Änderungen geben sie beim Speichern selbst frei.
3. **Metadaten in SQL, Inhalt in Blob Storage.** Die aktuelle freigegebene Version steht zusätzlich in den normalen Tabellen. Nur sie lesen die öffentlichen Seiten.
4. **Öffentliche History mit Datenschutz durch Voreinstellungen:** anonym, ausser die Person willigt ein. `noindex`.
5. **Zeitpunkt:** Datenmodell und Ablauf ab dem MVP. Die Registrierung öffnen wir in Phase 1.

## Optionen

### Option A: Snapshot als JSON in SQL

**Vorteile:**
- Eine Transaktion für Row und Inhalt.

**Nachteile:**
- Die Datenbank wächst mit jeder Version. Das Free Offer hat 32 GB und begrenzte vCore-Sekunden.

### Option B: Snapshot in Blob Storage (gewählt)

**Vorteile:**
- Die Datenbank bleibt klein. Blob Storage kostet für kleine JSON-Dateien fast nichts.
- Alte Versionen braucht nur die History. Sie lädt sie bei Bedarf.

**Nachteile:**
- Keine gemeinsame Transaktion. Darum gilt: zuerst den Blob schreiben, dann die Row. Ein Blob ohne Row wird aufgeräumt.
- Ein Vergleich zweier Versionen braucht zwei Blob-Reads.

### Option C: Änderungen pro Feld (Event Log)

**Nachteile:**
- Ein Snapshot lässt sich einfacher vergleichen, wiederherstellen und freigeben. Ein Vorschlag umfasst oft mehrere Felder.

### Option D: Direkt speichern ohne Freigabe (wie ein offenes Wiki)

**Nachteile:**
- Falsche oder gefährliche Angaben (z.B. zu Gefahren oder Verboten) wären sofort online.

## Konsequenzen

### Positive Auswirkungen

- Die Community kann beitragen, ohne dass die Qualität leidet.
- Jede Änderung ist nachvollziehbar und lässt sich wiederherstellen.
- Kein Umbau der Daten, wenn wir in Phase 1 die Registrierung öffnen.

### Negative Auswirkungen / Risiken

- Moderation braucht Zeit. Am Anfang macht Marc das allein.
- Ein Vorschlag auf einer veralteten Basis braucht Handarbeit. Es gibt keine automatische Zusammenführung.
- Ein Konto bedeutet Personendaten. Die Datenschutzerklärung muss Konto, Anzeigename und History beschreiben.
- Die Versionen sind unveränderlich. Einzige Ausnahme: Ein Moderator kann Personendaten im Inhalt schwärzen.

## Referenzen

- `cms.md` (Mitwirken und Freigabe, Datenschutz in der History)
- `datastructure.md` (`DiveSiteRevision`, Benutzer)
- `infrastructure.md` (Container `revisions`, `media-pending`)
