# ADR-012: Mail über Hostpoint SMTP

## Status

**Akzeptiert** (20.10.2026, Entscheid Marc)

## Kontext

- Die App versendet Mails über die Storage Queue und einen SMTP-Client (ADR-010). Lokal nimmt Mailpit die Mails an.
- Für Prod fehlte ein Dienst (offener Punkt #26).
- Marc hat bereits ein Hosting bei Hostpoint mit Postfächern für eigene Domains.
- Der Datenschutz muss den Dienst nennen, wenn er Daten ausserhalb der Schweiz bearbeitet.

## Entscheidung

Wir versenden in Prod über **Hostpoint SMTP**: `asmtp.mail.hostpoint.ch`, Port 587 mit STARTTLS, Anmeldung mit der E-Mail-Adresse des Postfachs. Benutzer und Passwort stehen im Key Vault (`Mail--UserName`, `Mail--Password`).

## Optionen

### Option A: Hostpoint SMTP

**Vorteile:**
- Besteht schon, keine zusätzlichen Kosten.
- Daten bleiben in der Schweiz.
- Kein neuer Code: Der bestehende SMTP-Client (MailKit) reicht.

**Nachteile:**
- Versandgrenzen des Hostings (für die erwarteten Mengen kein Problem).
- Zustellbarkeit hängt an SPF, DKIM und DMARC der Domain.

### Option B: Azure Communication Services Email

**Vorteile:** gut in Azure integriert, Managed Identity.

**Nachteile:** Domain-Verifizierung und eigener Client nötig. Daten in einem Azure-Dienst ausserhalb der Kontrolle von Marc.

## Konsequenzen

### Positive Auswirkungen

- Keine neue Abhängigkeit. Die Queue mit Retry und Dead-Letter (ADR-010) bleibt.
- Der Start bricht ab, wenn `Mail:UserName` gesetzt ist und `Mail:UseTls` fehlt. Das Passwort geht nie im Klartext hinaus.

### Negative Auswirkungen / Risiken

- SPF, DKIM und DMARC für `tauchplaetze.ch` müssen stimmen (doc/go-live.md).
- Fällt Hostpoint aus, bleiben die Mails in der Queue und gehen nach der Störung hinaus (Backoff bis 60 min).

## Referenzen

- [Hostpoint Support: E-Mail-Einstellungen auf einen Blick](https://support.hostpoint.ch/en/technical/e-mail/frequently-asked-questions/e-mail-settings-at-a-glance)
- ADR-010, `specs/infrastructure.md`
