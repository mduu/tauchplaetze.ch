# Design-Entwurf

Entwurf vom 17.10.2026. Zwei Seiten im dunklen Schema «Kaltwasser-Nacht». Alle Werte sind **Beispieldaten**. Fotos fehlen bewusst (Platzhalter).

| Datei | Inhalt |
|---|---|
| `start.html` | Startseite «Wo tauchen wir heute?» als statisches HTML. Direkt im Browser öffnen. |
| `tauchplatz.html` | Tauchplatzseite «Riff Williams, Herrliberg» als statisches HTML. |
| `source/start.dc.html` | Quelle der Startseite im Format des Design-Canvas (mit Tageswechsel als Logik) |
| `source/tauchplatz.dc.html` | Quelle der Tauchplatzseite |

## Hinweise für die Umsetzung

- Die Mockups zeigen Layout, Hierarchie, Farben und Typografie. Sie sind **kein Code-Vorbild**. Die Umsetzung erfolgt mit Blazor-Komponenten, Design Tokens (`../design-system.md`) und CSS Isolation.
- Alle Texte in den Mockups sind deutsch und fest eingetragen. In der App kommen sie aus `.resx` (vier Sprachen).
- Die statischen Dateien laden Schriften von Google Fonts. Die App hostet die Schriften selbst.
- Die `.dc.html`-Quellen enthalten Platzhalter (`{{…}}`), Schleifen (`<sc-for>`) und Bedingungen (`<sc-if>`). Die Logik mit den Beispieldaten steht im Skript am Ende der Datei.
- Die Platzbeschreibung von «Riff Williams» stammt sinngemäss aus einer öffentlichen Tauchplatzliste. Für die echte Plattform schreibt Marc eigene Texte.

## Was der Entwurf zeigt

**Startseite:** Frage als Titel, Tagesauswahl, «Beste Wahl» mit Seequerschnitt (Oberflächentemperatur, 20 m, Sprungschicht), Rangliste im Umkreis, Erklärung der Datenquellen, Seen-Übersicht, Buddy-Block, Fuss mit Quellen.

**Tauchplatzseite:** Hero mit Unterwasser-Silhouette, Tags, Tauchbedingungen mit Temperaturprofil und Sprungschicht, Kacheln (Sicht-Indikator, Wind, Algen; der Anzug aus dem Mockup entfällt), 5-Tage-Verlauf, Beschreibung mit Foto-Platzhaltern, Tauchplatzskizze, Praktisch-Block mit Partnerplatz, Meldungen, Buddy-Block.
