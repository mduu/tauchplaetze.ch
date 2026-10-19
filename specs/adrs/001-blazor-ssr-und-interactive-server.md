# ADR-001: Blazor Web App mit Static SSR und Interactive Server

## Status

**Akzeptiert** (17.10.2026)

## Kontext

- Die öffentlichen Seiten müssen schnell, cachebar und gut für Suchmaschinen sein.
- Der Redaktionsbereich braucht reiche Editoren (Karte mit Pin, Medien sortieren, Revisionen vergleichen). Er hat nur wenige Benutzer.
- Marc ist .NET-Experte. Ein JavaScript-Framework mit SSR (Next.js) bringt nur Komplexität.
- Zur Wahl standen Razor Pages, Blazor Static SSR, Blazor Interactive (Server, WebAssembly) und React/Next.js.

## Entscheidung

Wir entscheiden uns für **eine Blazor Web App mit gemischten Render-Modi**:

- Öffentliche Seiten: **Static SSR** mit Enhanced Navigation und Streaming Rendering.
- Redaktionsbereich `/admin`: **Interactive Server**.
- Kein Interactive WebAssembly im MVP.

## Optionen

### Option A: Razor Pages

**Beschreibung:** Seitenzentriert, `.cshtml` plus `PageModel` mit `OnGet`/`OnPost`.

**Vorteile:**
- Ausgereift, schlank, sehr gut für SSR.

**Nachteile:**
- Wiederverwendung nur über Partials und View Components.
- Interaktivität im Admin nur mit eigenem JavaScript.

### Option B: Blazor Web App, gemischte Render-Modi (gewählt)

**Beschreibung:** Razor-Komponenten. Statisch auf dem Server gerendert. Interaktivität pro Komponente oder Bereich zuschaltbar.

**Vorteile:**
- Ein Komponentenmodell für öffentliche Seiten und Admin.
- Öffentlich reines HTML, cachebar, keine Verbindung pro Besucher.
- Admin interaktiv ohne JavaScript-Framework.

**Nachteile:**
- Static SSR hat Einschränkungen (keine Events ohne Interaktivität). Kleine Interaktionen lösen wir mit Links, Formularen oder wenig JavaScript.
- Interactive Server braucht eine SignalR-Verbindung. Für wenige Redaktoren unkritisch.

### Option C: React mit Next.js

**Vorteile:**
- Grosses Ökosystem.

**Nachteile:**
- Zweite Sprache und zweiter Laufzeitstack. Mehr Komplexität. Kein Vorteil für dieses Projekt.

## Konsequenzen

### Positive Auswirkungen

- Schnelle öffentliche Seiten mit Output Cache.
- Ein Stack (C#) von der Datenbank bis zur Oberfläche.

### Negative Auswirkungen / Risiken

- Interaktive Elemente auf öffentlichen Seiten brauchen Disziplin (SSR-tauglich bauen).
- Interactive Server auf öffentlichen Seiten ist verboten (Kosten, Skalierung).

## Referenzen

- `architecture.md`, Abschnitt «Render-Modi»

## Nachtrag 17.10.2026

Seit ADR-008 arbeiten auch Mitwirkende im CMS (Interactive Server). Es bleiben wenige gleichzeitige Verbindungen. Steigt die Zahl stark, prüfen wir das Formular für Vorschläge als Static SSR.

## Nachtrag 19.10.2026

Mitwirkende bearbeiten einen Platz im Bearbeiten-Modus der Website (`/{lang}/tauchplaetze/{slug}/bearbeiten`). Diese eine Seite ist Interactive Server, aber im Layout der Website und ohne MudBlazor. Sie liegt hinter der Anmeldung: Besucher bekommen weiter keine Verbindung. Die Regel «kein Interactive Server auf öffentlichen Seiten» gilt für alle Seiten ohne Anmeldung. Moderation (`/admin`) und Plattform (`/platform`) bleiben Interactive Server mit MudBlazor.
